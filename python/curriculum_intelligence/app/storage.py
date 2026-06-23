"""Object storage abstraction over the ``curriculum`` MinIO bucket.

A ``Protocol`` lets tests substitute an in-memory stub (so CI does not need a
real MinIO container) while the contract test still exercises the real Postgres
claim. ``object_key`` is treated as an opaque, path-safe storage key: the worker
never shells out on it and never uses it as a filesystem path.
"""

from __future__ import annotations

import io
from typing import Protocol, runtime_checkable

from .config import StorageConfig
from .logging import get_logger

logger = get_logger(__name__)


def assert_safe_object_key(object_key: str) -> str:
    """Reject keys that could escape the bucket namespace or be abused as paths.

    The .NET upload side writes GUID-based keys, but the worker still validates
    defensively because the payload crosses a process boundary.
    """

    if not object_key or not object_key.strip():
        raise ValueError("object_key must be a non-empty string")
    key = object_key.strip()
    if key.startswith("/") or key.startswith("\\"):
        raise ValueError(f"object_key must not be absolute: {key!r}")
    # Block path traversal in any segment.
    for sep in ("/", "\\"):
        if any(seg in {"..", "."} for seg in key.split(sep)):
            raise ValueError(f"object_key must not contain traversal segments: {key!r}")
    if "\x00" in key:
        raise ValueError("object_key must not contain null bytes")
    return key


@runtime_checkable
class ObjectStore(Protocol):
    """Minimal object-store contract: get bytes, put bytes."""

    def get_object(self, object_key: str) -> bytes: ...

    def put_object(
        self, object_key: str, data: bytes, content_type: str = "application/octet-stream"
    ) -> None: ...


class MinioObjectStore:
    """boto3 S3 client pointed at MinIO. Created lazily so importing this module
    never requires boto3 at test-collection time."""

    def __init__(self, config: StorageConfig) -> None:
        self._config = config
        self._client = None

    def _get_client(self):
        if self._client is None:
            import boto3  # imported lazily — not needed by the stub-based tests
            from botocore.config import Config as BotoConfig

            self._client = boto3.client(
                "s3",
                endpoint_url=self._config.endpoint_url,
                aws_access_key_id=self._config.access_key,
                aws_secret_access_key=self._config.secret_key,
                region_name=self._config.region,
                config=BotoConfig(signature_version="s3v4", s3={"addressing_style": "path"}),
            )
        return self._client

    def get_object(self, object_key: str) -> bytes:
        key = assert_safe_object_key(object_key)
        client = self._get_client()
        logger.info("downloading object bucket=%s key=%s", self._config.bucket, key)
        resp = client.get_object(Bucket=self._config.bucket, Key=key)
        body = resp["Body"]
        try:
            return body.read()
        finally:
            body.close()

    def put_object(
        self, object_key: str, data: bytes, content_type: str = "application/octet-stream"
    ) -> None:
        key = assert_safe_object_key(object_key)
        client = self._get_client()
        logger.info(
            "uploading object bucket=%s key=%s bytes=%d", self._config.bucket, key, len(data)
        )
        client.put_object(
            Bucket=self._config.bucket,
            Key=key,
            Body=io.BytesIO(data),
            ContentType=content_type,
        )


class InMemoryObjectStore:
    """Deterministic in-memory store for tests/CI (no MinIO container required)."""

    def __init__(self) -> None:
        self._objects: dict[str, bytes] = {}

    def get_object(self, object_key: str) -> bytes:
        key = assert_safe_object_key(object_key)
        if key not in self._objects:
            raise KeyError(f"object not found: {key}")
        return self._objects[key]

    def put_object(
        self, object_key: str, data: bytes, content_type: str = "application/octet-stream"
    ) -> None:
        key = assert_safe_object_key(object_key)
        self._objects[key] = data

    def seed(self, object_key: str, data: bytes) -> None:
        self._objects[assert_safe_object_key(object_key)] = data

    @property
    def keys(self) -> list[str]:
        return list(self._objects.keys())
