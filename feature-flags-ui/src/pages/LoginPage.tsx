import React, { useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { loginThunk } from '../features/auth/authSlice';
import { AppDispatch, RootState } from '../app/store';

export default function LoginPage() {
  const dispatch = useDispatch<AppDispatch>();
  const navigate = useNavigate();
  const { loading, error } = useSelector((s: RootState) => s.auth);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const result = await dispatch(loginThunk({ email, password }));
    if (loginThunk.fulfilled.match(result)) navigate('/');
  };

  return (
    <div style={styles.wrap}>
      <div style={styles.card}>
        <h1 style={styles.title}>Feature Flag Console</h1>
        <p style={styles.sub}>UC117 – Admin Console</p>
        <form onSubmit={handleSubmit}>
          <div style={styles.field}>
            <label style={styles.label}>Email</label>
            <input style={styles.input} type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div style={styles.field}>
            <label style={styles.label}>Password</label>
            <input style={styles.input} type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </div>
          {error && <div style={styles.error}>{error}</div>}
          <button style={styles.btn} type="submit" disabled={loading}>
            {loading ? 'Signing in…' : 'Sign In'}
          </button>
        </form>
        <div style={styles.hint}>
          <strong>Demo accounts:</strong><br />
          admin@flagconsole.dev / Admin@123! (Admin)<br />
          approver@flagconsole.dev / Approver@123! (Approver)<br />
          dev1@flagconsole.dev / Dev@123! (Developer)
        </div>
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  wrap: { display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', background: '#f0f2f5' },
  card: { background: '#fff', padding: 40, borderRadius: 10, boxShadow: '0 2px 20px rgba(0,0,0,.12)', width: 380 },
  title: { margin: '0 0 4px', color: '#0D8390', fontSize: 24, fontWeight: 700 },
  sub: { margin: '0 0 28px', color: '#888', fontSize: 13 },
  field: { marginBottom: 16 },
  label: { display: 'block', marginBottom: 6, fontSize: 13, fontWeight: 500, color: '#444' },
  input: { width: '100%', padding: '10px 12px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14 },
  btn: { width: '100%', padding: '11px', background: '#0D8390', color: '#fff', border: 'none', borderRadius: 6, fontSize: 15, fontWeight: 600, cursor: 'pointer', marginTop: 8 },
  error: { background: '#fff0f0', border: '1px solid #ffcdd2', color: '#c62828', padding: '8px 12px', borderRadius: 6, marginBottom: 12, fontSize: 13 },
  hint: { marginTop: 24, fontSize: 12, color: '#888', background: '#f7f9fa', padding: '10px 12px', borderRadius: 6, lineHeight: 1.7 },
};
