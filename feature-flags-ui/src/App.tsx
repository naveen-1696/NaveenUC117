import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useSelector } from 'react-redux';
import { RootState } from './app/store';
import LoginPage from './pages/LoginPage';
import FlagsPage from './pages/FlagsPage';
import FlagEnvironmentPage from './pages/FlagEnvironmentPage';
import ChangeRequestsPage from './pages/ChangeRequestsPage';
import ApiKeysPage from './pages/ApiKeysPage';
import Layout from './components/Layout';

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { token } = useSelector((s: RootState) => s.auth);
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { role } = useSelector((s: RootState) => s.auth);
  if (role !== 'Admin') return <Navigate to="/" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<RequireAuth><Layout><FlagsPage /></Layout></RequireAuth>} />
        <Route path="/flags/:flagId/environments/:envId" element={<RequireAuth><Layout><FlagEnvironmentPage /></Layout></RequireAuth>} />
        <Route path="/change-requests" element={<RequireAuth><Layout><ChangeRequestsPage /></Layout></RequireAuth>} />
        <Route path="/api-keys" element={<RequireAuth><RequireAdmin><Layout><ApiKeysPage /></Layout></RequireAdmin></RequireAuth>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
