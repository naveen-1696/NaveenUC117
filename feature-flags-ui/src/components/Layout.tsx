import React from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useDispatch, useSelector } from 'react-redux';
import { logout } from '../features/auth/authSlice';
import { AppDispatch, RootState } from '../app/store';

export default function Layout({ children }: { children: React.ReactNode }) {
  const dispatch = useDispatch<AppDispatch>();
  const navigate = useNavigate();
  const location = useLocation();
  const { email, role } = useSelector((s: RootState) => s.auth);

  const handleLogout = () => { dispatch(logout()); navigate('/login'); };

  const nav = [
    { to: '/', label: 'Flags', icon: '⚑' },
    { to: '/change-requests', label: 'Approvals', icon: '✓' },
    { to: '/api-keys', label: 'API Keys', icon: '🔑', admin: true },
  ];

  return (
    <div style={styles.root}>
      <aside style={styles.sidebar}>
        <div style={styles.logo}>
          <span style={styles.logoText}>FlagConsole</span>
          <span style={styles.logoBadge}>UC117</span>
        </div>
        <nav style={styles.nav}>
          {nav.filter(n => !n.admin || role === 'Admin').map((n) => (
            <Link
              key={n.to}
              to={n.to}
              style={{ ...styles.navLink, ...(location.pathname === n.to ? styles.navActive : {}) }}
            >
              <span style={styles.navIcon}>{n.icon}</span> {n.label}
            </Link>
          ))}
        </nav>
        <div style={styles.userBox}>
          <div style={styles.userEmail}>{email}</div>
          <div style={styles.userRole}>{role}</div>
          <button style={styles.logoutBtn} onClick={handleLogout}>Log out</button>
        </div>
      </aside>
      <main style={styles.main}>{children}</main>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  root: { display: 'flex', minHeight: '100vh' },
  sidebar: { width: 220, background: '#0b4f57', color: '#fff', display: 'flex', flexDirection: 'column', padding: '0 0 20px' },
  logo: { padding: '24px 20px 20px', borderBottom: '1px solid rgba(255,255,255,.1)' },
  logoText: { display: 'block', fontSize: 20, fontWeight: 700, color: '#fff' },
  logoBadge: { fontSize: 11, background: '#0D8390', borderRadius: 4, padding: '1px 6px', color: '#fff' },
  nav: { flex: 1, padding: '16px 0' },
  navLink: { display: 'flex', alignItems: 'center', gap: 10, padding: '10px 20px', color: 'rgba(255,255,255,.8)', textDecoration: 'none', fontSize: 14, fontWeight: 500, transition: 'background .15s' },
  navActive: { background: 'rgba(255,255,255,.12)', color: '#fff' },
  navIcon: { fontSize: 16 },
  userBox: { padding: '16px 20px', borderTop: '1px solid rgba(255,255,255,.1)' },
  userEmail: { fontSize: 12, color: 'rgba(255,255,255,.7)', wordBreak: 'break-all' },
  userRole: { fontSize: 11, color: '#7dcdd3', marginTop: 2, marginBottom: 10 },
  logoutBtn: { background: 'rgba(255,255,255,.1)', color: '#fff', border: 'none', borderRadius: 4, padding: '6px 12px', cursor: 'pointer', fontSize: 12, width: '100%' },
  main: { flex: 1, padding: 32, overflowY: 'auto' },
};
