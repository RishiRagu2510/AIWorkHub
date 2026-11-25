import { Suspense, useEffect, useState } from 'react';
import { createBrowserRouter, createRoutesFromElements, Navigate, Route, RouterProvider } from 'react-router';
import { Provider } from 'react-redux';
import { store } from './app/store';
import { AuthProvider } from './features/auth/AuthProvider';
import { LoadingState, NoticeProvider } from './components/common';
import { ProtectedRoute, RouteErrorFallback } from './App';
import Login from './pages/Login';
import AuthCallback from './pages/AuthCallback';
import VerifyEmail from './pages/VerifyEmail';
import LaunchPad from './pages/LaunchPad';
import LandingPlan from './pages/LandingPlan';

/**
 * The landing site: sign in, then pick a portal. It is not a portal itself, so it has no licence of its own and ships none of
 * the portals' pages. Anything it does not know goes back to the launch page.
 */
export default function LandingApp() {
  const [router] = useState(() =>
    createBrowserRouter(
      createRoutesFromElements(
        <Route errorElement={<RouteErrorFallback />}>
          <Route path="/verify-email" element={<VerifyEmail />} />
          <Route path="/login" element={<Login />} />
          <Route path="/auth/callback" element={<AuthCallback />} />
          <Route element={<ProtectedRoute />}>
            <Route index element={<LaunchPad />} />
            <Route path="plan" element={<LandingPlan />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Route>,
      ),
    ),
  );
  useEffect(() => () => router.dispose(), [router]);
  return (
    <Provider store={store}>
      <AuthProvider>
        <NoticeProvider>
          <Suspense fallback={<LoadingState />}>
            <RouterProvider router={router} />
          </Suspense>
        </NoticeProvider>
      </AuthProvider>
    </Provider>
  );
}
