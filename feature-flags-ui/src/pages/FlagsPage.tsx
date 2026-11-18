import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Link } from 'react-router-dom';
import { AppDispatch, RootState } from '../app/store';
import { fetchFlags, fetchEnvironments, createFlag, archiveFlag, setSearch, setPage } from '../features/flags/flagsSlice';

export default function FlagsPage() {
  const dispatch = useDispatch<AppDispatch>();
  const { items, totalCount, page, pageSize, search, loading, error, environments } = useSelector((s: RootState) => s.flags);
  const { role } = useSelector((s: RootState) => s.auth);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState({ key: '', name: '', description: '', type: 'Boolean' });
  const [createError, setCreateError] = useState('');
  const [localSearch, setLocalSearch] = useState(search);

  useEffect(() => {
    dispatch(fetchFlags({ page, pageSize, search }));
    dispatch(fetchEnvironments());
  }, [dispatch, page, pageSize, search]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    dispatch(setSearch(localSearch));
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError('');
    const result = await dispatch(createFlag(form));
    if (createFlag.rejected.match(result)) {
      setCreateError(result.payload as string);
    } else {
      setShowCreate(false);
      setForm({ key: '', name: '', description: '', type: 'Boolean' });
    }
  };

  const handleArchive = async (id: number, name: string) => {
    if (!window.confirm(`Archive flag "${name}"? It will be deactivated everywhere.`)) return;
    dispatch(archiveFlag(id));
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div>
      <div style={styles.header}>
        <h2 style={styles.h2}>Feature Flags</h2>
        {(role === 'Developer' || role === 'Admin') && (
          <button style={styles.btn} onClick={() => setShowCreate(true)}>+ New Flag</button>
        )}
      </div>

      {/* Search */}
      <form onSubmit={handleSearch} style={{ marginBottom: 20 }}>
        <input
          style={styles.searchInput}
          placeholder="Search flags…"
          value={localSearch}
          onChange={(e) => setLocalSearch(e.target.value)}
        />
        <button style={{ ...styles.btn, marginLeft: 8 }} type="submit">Search</button>
        {search && <button style={{ ...styles.btn, marginLeft: 4, background: '#888' }} type="button" onClick={() => { setLocalSearch(''); dispatch(setSearch('')); }}>Clear</button>}
      </form>

      {/* Create Modal */}
      {showCreate && (
        <div style={styles.modal}>
          <div style={styles.modalContent}>
            <h3 style={styles.h3}>Create Flag</h3>
            <form onSubmit={handleCreate}>
              {[
                { label: 'Key (lowercase, hyphens)', field: 'key', placeholder: 'my-feature-flag' },
                { label: 'Name', field: 'name', placeholder: 'My Feature Flag' },
                { label: 'Description', field: 'description', placeholder: 'Optional description' },
              ].map(({ label, field, placeholder }) => (
                <div key={field} style={styles.field}>
                  <label style={styles.label}>{label}</label>
                  <input style={styles.input} placeholder={placeholder} value={(form as any)[field]}
                    onChange={(e) => setForm({ ...form, [field]: e.target.value })}
                    required={field !== 'description'} />
                </div>
              ))}
              <div style={styles.field}>
                <label style={styles.label}>Type</label>
                <select style={styles.input} value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
                  <option>Boolean</option>
                  <option>String</option>
                  <option>Number</option>
                </select>
              </div>
              {createError && <div style={styles.error}>{createError}</div>}
              <div style={{ display: 'flex', gap: 8 }}>
                <button style={styles.btn} type="submit">Create</button>
                <button style={{ ...styles.btn, background: '#888' }} type="button" onClick={() => setShowCreate(false)}>Cancel</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Flags Table */}
      {loading && <div style={styles.loading}>Loading…</div>}
      {error && <div style={styles.error}>{error}</div>}
      {!loading && items.length === 0 && <div style={styles.empty}>No flags found.</div>}

      {!loading && items.length > 0 && (
        <table style={styles.table}>
          <thead>
            <tr>
              {['Key', 'Name', 'Type', 'Status', 'Environments', 'Actions'].map((h) => (
                <th key={h} style={styles.th}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {items.map((flag) => (
              <tr key={flag.id} style={{ opacity: flag.isArchived ? 0.5 : 1 }}>
                <td style={styles.td}><code style={styles.code}>{flag.key}</code></td>
                <td style={styles.td}>{flag.name}</td>
                <td style={styles.td}><span style={styles.badge}>{flag.type}</span></td>
                <td style={styles.td}>{flag.isArchived ? <span style={styles.archived}>Archived</span> : <span style={styles.active}>Active</span>}</td>
                <td style={styles.td}>
                  {environments.map((env) => (
                    <Link key={env.id} to={`/flags/${flag.id}/environments/${env.id}`} style={styles.envLink}>
                      {env.name}{env.isProtected ? ' 🔒' : ''}
                    </Link>
                  ))}
                </td>
                <td style={styles.td}>
                  {!flag.isArchived && role === 'Admin' && (
                    <button style={styles.dangerBtn} onClick={() => handleArchive(flag.id, flag.name)}>Archive</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div style={styles.pagination}>
          {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
            <button key={p} style={{ ...styles.pageBtn, ...(p === page ? styles.pageActive : {}) }}
              onClick={() => dispatch(setPage(p))}>{p}</button>
          ))}
        </div>
      )}
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 },
  h2: { margin: 0, fontSize: 24, color: '#1a1a1a' },
  h3: { margin: '0 0 20px', color: '#0D8390' },
  btn: { background: '#0D8390', color: '#fff', border: 'none', borderRadius: 6, padding: '9px 18px', cursor: 'pointer', fontSize: 14, fontWeight: 600 },
  dangerBtn: { background: '#c62828', color: '#fff', border: 'none', borderRadius: 4, padding: '5px 10px', cursor: 'pointer', fontSize: 12 },
  searchInput: { padding: '9px 14px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14, width: 280 },
  table: { width: '100%', borderCollapse: 'collapse', background: '#fff', borderRadius: 8, overflow: 'hidden', boxShadow: '0 1px 6px rgba(0,0,0,.08)' },
  th: { background: '#f7f9fa', padding: '12px 16px', textAlign: 'left', fontSize: 12, fontWeight: 600, color: '#555', borderBottom: '1px solid #eee' },
  td: { padding: '12px 16px', fontSize: 14, borderBottom: '1px solid #f0f0f0', color: '#333' },
  code: { background: '#f0f4f4', padding: '2px 6px', borderRadius: 4, fontFamily: 'monospace', fontSize: 13 },
  badge: { background: '#e6f3f5', color: '#0b4f57', borderRadius: 4, padding: '2px 8px', fontSize: 12 },
  active: { color: '#2e7d32', fontWeight: 600 },
  archived: { color: '#888' },
  envLink: { display: 'inline-block', background: '#e6f3f5', color: '#0b6a74', padding: '2px 8px', borderRadius: 4, fontSize: 12, marginRight: 4, textDecoration: 'none' },
  modal: { position: 'fixed', inset: 0, background: 'rgba(0,0,0,.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100 },
  modalContent: { background: '#fff', borderRadius: 10, padding: 32, width: 460, boxShadow: '0 8px 32px rgba(0,0,0,.2)' },
  field: { marginBottom: 16 },
  label: { display: 'block', marginBottom: 6, fontSize: 13, fontWeight: 500, color: '#444' },
  input: { width: '100%', padding: '10px 12px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14 },
  error: { background: '#fff0f0', border: '1px solid #ffcdd2', color: '#c62828', padding: '8px 12px', borderRadius: 6, marginBottom: 12, fontSize: 13 },
  loading: { color: '#888', padding: 24 },
  empty: { color: '#888', padding: 24, textAlign: 'center' },
  pagination: { display: 'flex', gap: 6, marginTop: 20 },
  pageBtn: { border: '1px solid #d9d9d9', background: '#fff', borderRadius: 4, padding: '6px 12px', cursor: 'pointer' },
  pageActive: { background: '#0D8390', color: '#fff', border: '1px solid #0D8390' },
};
