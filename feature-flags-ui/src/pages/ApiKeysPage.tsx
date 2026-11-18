import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { AppDispatch, RootState } from '../app/store';
import { fetchApiKeys, revokeApiKey } from '../features/apiKeys/apiKeysSlice';
import { fetchEnvironments } from '../features/flags/flagsSlice';
import * as api from '../api/apiClient';

export default function ApiKeysPage() {
  const dispatch = useDispatch<AppDispatch>();
  const { items, loading } = useSelector((s: RootState) => s.apiKeys);
  const { environments } = useSelector((s: RootState) => s.flags);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState({ environmentId: '', name: '' });
  const [newKey, setNewKey] = useState<{ plainKey: string; name: string } | null>(null);

  useEffect(() => {
    dispatch(fetchApiKeys());
    dispatch(fetchEnvironments());
  }, [dispatch]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    const { data } = await api.createApiKey({ environmentId: parseInt(form.environmentId), name: form.name });
    setNewKey({ plainKey: data.plainKey, name: data.name });
    setShowCreate(false);
    dispatch(fetchApiKeys());
  };

  const handleRevoke = async (id: number) => {
    if (!window.confirm('Revoke this API key? Any clients using it will immediately lose access.')) return;
    dispatch(revokeApiKey(id));
  };

  return (
    <div>
      <div style={S.header}>
        <h2 style={S.h2}>API Keys</h2>
        <button style={S.btn} onClick={() => setShowCreate(true)}>+ New API Key</button>
      </div>
      <p style={S.desc}>API keys authenticate the evaluation endpoint (<code>/evaluate/{'{env}'}/…</code>). Store only hashed values — the plain key is shown once.</p>

      {newKey && (
        <div style={S.keyReveal}>
          <strong>✓ Key created: {newKey.name}</strong><br />
          <span style={S.warningText}>Copy this key now — it will not be shown again.</span><br />
          <code style={S.plainKey}>{newKey.plainKey}</code>
          <button style={S.copyBtn} onClick={() => { navigator.clipboard.writeText(newKey.plainKey); }}>Copy</button>
          <button style={S.dismissBtn} onClick={() => setNewKey(null)}>Dismiss</button>
        </div>
      )}

      {showCreate && (
        <div style={S.modal}>
          <div style={S.modalContent}>
            <h3 style={S.h3}>Create API Key</h3>
            <form onSubmit={handleCreate}>
              <div style={S.field}>
                <label style={S.label}>Environment</label>
                <select style={S.select} value={form.environmentId} onChange={(e) => setForm({ ...form, environmentId: e.target.value })} required>
                  <option value="">Select environment…</option>
                  {environments.map((env) => (
                    <option key={env.id} value={env.id}>{env.name}</option>
                  ))}
                </select>
              </div>
              <div style={S.field}>
                <label style={S.label}>Key Name</label>
                <input style={S.input} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="e.g. mobile-app-prod" required />
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button style={S.btn} type="submit">Create</button>
                <button style={{ ...S.btn, background: '#888' }} type="button" onClick={() => setShowCreate(false)}>Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {loading && <div style={{ color: '#888', padding: 16 }}>Loading…</div>}

      <table style={S.table}>
        <thead>
          <tr>{['Name', 'Environment', 'Status', 'Created', 'Action'].map(h => <th key={h} style={S.th}>{h}</th>)}</tr>
        </thead>
        <tbody>
          {items.map((key) => (
            <tr key={key.id}>
              <td style={S.td}>{key.name}</td>
              <td style={S.td}><span style={S.envBadge}>{key.environmentName}</span></td>
              <td style={S.td}>{key.isActive ? <span style={S.active}>Active</span> : <span style={S.revoked}>Revoked</span>}</td>
              <td style={S.td}>{new Date(key.createdAt).toLocaleString()}</td>
              <td style={S.td}>
                {key.isActive && (
                  <button style={S.revokeBtn} onClick={() => handleRevoke(key.id)}>Revoke</button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const S: Record<string, React.CSSProperties> = {
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 },
  h2: { margin: 0, fontSize: 24 },
  h3: { margin: '0 0 20px', color: '#0D8390' },
  desc: { color: '#666', fontSize: 14, marginBottom: 24 },
  btn: { background: '#0D8390', color: '#fff', border: 'none', borderRadius: 6, padding: '9px 18px', cursor: 'pointer', fontSize: 14, fontWeight: 600 },
  revokeBtn: { background: '#c62828', color: '#fff', border: 'none', borderRadius: 4, padding: '5px 10px', cursor: 'pointer', fontSize: 12 },
  keyReveal: { background: '#f0fff4', border: '1px solid #c8e6c9', borderRadius: 8, padding: 16, marginBottom: 20, fontSize: 14 },
  warningText: { color: '#c62828', fontSize: 13 },
  plainKey: { display: 'block', background: '#1a1a1a', color: '#7af0a1', padding: '10px 14px', borderRadius: 6, fontFamily: 'monospace', fontSize: 13, margin: '10px 0', wordBreak: 'break-all' },
  copyBtn: { background: '#0D8390', color: '#fff', border: 'none', borderRadius: 4, padding: '5px 12px', cursor: 'pointer', marginRight: 8 },
  dismissBtn: { background: '#888', color: '#fff', border: 'none', borderRadius: 4, padding: '5px 12px', cursor: 'pointer' },
  modal: { position: 'fixed', inset: 0, background: 'rgba(0,0,0,.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 },
  modalContent: { background: '#fff', borderRadius: 10, padding: 32, width: 420, boxShadow: '0 8px 32px rgba(0,0,0,.2)' },
  field: { marginBottom: 16 },
  label: { display: 'block', marginBottom: 6, fontSize: 13, fontWeight: 500 },
  input: { width: '100%', padding: '10px 12px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14 },
  select: { width: '100%', padding: '10px 12px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14 },
  table: { width: '100%', borderCollapse: 'collapse', background: '#fff', borderRadius: 8, overflow: 'hidden', boxShadow: '0 1px 6px rgba(0,0,0,.08)' },
  th: { background: '#f7f9fa', padding: '12px 16px', textAlign: 'left', fontSize: 12, fontWeight: 600, color: '#555', borderBottom: '1px solid #eee' },
  td: { padding: '12px 16px', fontSize: 14, borderBottom: '1px solid #f0f0f0' },
  envBadge: { background: '#e6f3f5', color: '#0b4f57', borderRadius: 4, padding: '2px 8px', fontSize: 12 },
  active: { color: '#2e7d32', fontWeight: 600 },
  revoked: { color: '#888' },
};
