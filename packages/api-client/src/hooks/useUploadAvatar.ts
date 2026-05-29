/**
 * useUploadAvatar — POST /api/Users/Account/Avatar (authenticated).
 *
 * Wraps the NSwag `avatarPOST` method (multipart/form-data). Takes a
 * `FileParameter` (the generated client's `{ data: Blob; fileName: string }`
 * shape) and unwraps to the typed `AvatarUploadResponse` which carries the new
 * `avatarUrl`. On success invalidates myProfile + the Me query so both refresh
 * with the new URL. Drives the Settings → Profile avatar Upload button.
 *
 * Callers on web can build the FileParameter from a native File/Blob:
 *   mutate({ data: file, fileName: file.name })
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
