import { redirect } from 'next/navigation';

/**
 * Root entry — send everyone to the dashboard. The client-side admin guard in
 * `app/(admin)/layout.tsx` then resolves auth: unauthenticated / non-admin
 * visitors are bounced to `/login`, signed-in admins stay on the shell.
 */
export default function RootPage() {
  redirect('/dashboard');
}
