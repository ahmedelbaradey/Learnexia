/**
 * refresh-swagger — re-pull the committed OpenAPI snapshot from the live API.
 *
 * The typed contract for @learnexia/api-client is generated from a COMMITTED
 * `swagger.json` snapshot (so CI/type-gen needs no running backend). When the
 * backend contract changes, re-pull the snapshot, then regenerate types:
 *
 *   corepack pnpm --filter @learnexia/api-client refresh:swagger
 *   corepack pnpm --filter @learnexia/api-client gen:api
 *
 * Equivalent manual command (the source of truth for the URL + flags):
 *
 *   curl -sk https://localhost:7080/swagger/v2/swagger.json -o swagger.json
 *
 * `-k` is required because the local backend uses a dev self-signed cert.
 * Override the URL with SWAGGER_URL if the host/port differs.
 */
import { writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const SWAGGER_URL =
  process.env.SWAGGER_URL ?? 'https://localhost:7080/swagger/v2/swagger.json';

const here = dirname(fileURLToPath(import.meta.url));
const out = resolve(here, '..', 'swagger.json');

async function main() {
  // Accept the local dev self-signed cert (mirrors curl -k). Scoped to this
  // process only; never set globally.
  const agentOpts = SWAGGER_URL.startsWith('https://localhost')
    ? { tls: { rejectUnauthorized: false } }
    : undefined;

  // Node 20+ global fetch. For localhost https we relax cert validation via env
  // to avoid pulling an https-agent dependency into this dev-only script.
  if (agentOpts) process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

  const res = await fetch(SWAGGER_URL);
  if (!res.ok) {
    throw new Error(`Failed to fetch swagger: ${res.status} ${res.statusText}`);
  }
  const json = await res.json();
  await writeFile(out, `${JSON.stringify(json, null, 2)}\n`, 'utf8');
  console.log(`Wrote ${out} from ${SWAGGER_URL}`);
  console.log('Next: corepack pnpm --filter @learnexia/api-client gen:api');
}

main().catch((err) => {
  console.error(err.message);
  process.exit(1);
});
