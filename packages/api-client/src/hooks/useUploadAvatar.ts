/**
 * useUploadAvatar — POST /api/Users/Account/Avatar (authenticated, multipart).
 *
 * Wraps the NSwag `avatarPOST(FileParameter)` method. On success it invalidates
 * the account-profile query and the `Me` query so the `Avatar` component
 * re-renders with the new `avatarUrl` without a manual refresh.
 *
 * The caller builds the `FileParameter` from the chosen file:
 *   `{ data: file, fileName: file.name }` (web Blob/File).
 * Client-side type/size validation should be done BEFORE calling `mutate`.
 *
 * Security: no file content is logged. Returned `avatarUrl` is a MinIO
 * presigned URL sourced exclusively from the backend; never constructed
 * client-side.
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { AvatarUploadResponse, FileParameter } from '../schemas';

export function useUploadAvatar(): UseMutationResult<
  AvatarUploadResponse,
  Error,
  FileParameter
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: FileParameter): Promise<AvatarUploadResponse> =>
      unwrapEnvelope(client.avatarPOST(file)) as Promise<AvatarUploadResponse>,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.account.profile(),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() }),
      ]);
    },
  });
}
