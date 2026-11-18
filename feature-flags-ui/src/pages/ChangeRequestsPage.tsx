import React, { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { AppDispatch, RootState } from '../app/store';
import { fetchChangeRequests, approveRequest, rejectRequest } from '../features/changeRequests/changeRequestsSlice';

export default function ChangeRequestsPage() {
  const dispatch = useDispatch<AppDispatch>();
  const { items, loading, error } = useSelector((s: RootState) => s.changeRequests);
  const { role, userId } = useSelector((s: RootState) => s.auth);
  const [reviewNote, setReviewNote] = useState<Record<number, string>>({});
  const [filterStatus, setFilterStatus] = useState<string>('pending');
  const [actionError, setActionError] = useState('');

  useEffect(() => {
    dispatch(fetchChangeRequests(filterStatus || undefined));
  }, [dispatch, filterStatus]);

  const handleApprove = async (id: number, requestedById: string) => {
    setActionError('');
    if (requestedById === userId) { setActionError('You cannot approve your own change request.'); return; }
    if (!window.confirm('Approve this change? It will be applied immediately.')) return;
    const r = await dispatch(approveRequest({ id, reviewNote: reviewNote[id] }));
    if (approveRequest.rejected.match(r)) setActionError(r.payload as string);
    else dispatch(fetchChangeRequests(filterStatus || undefined));
  };

  const handleReject = async (id: number, requestedById: string) => {
    setActionError('');
    if (requestedById === userId) { setActionError('You cannot reject your own change request.'); return; }
    if (!window.confirm('Reject this change?')) return;
    const r = await dispatch(rejectRequest({ id, reviewNote: reviewNote[id] }));
    if (rejectRequest.rejected.match(r)) setActionError(r.payload as string);
    else dispatch(fetchChangeRequests(filterStatus || undefined));
  };

  const statusColors: Record<string, React.CSSProperties> = {
    Pending: { background: '#fff8e1', color: '#f57f17' },
    Approved: { background: '#f0fff4', color: '#2e7d32' },
    Rejected: { background: '#fff0f0', color: '#c62828' },
  };

  return (
    <div>
      <div style={S.header}>
        <h2 style={S.h2}>Change Requests</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          {['pending', 'all'].map((s) => (
            <button key={s} style={{ ...S.filterBtn, ...(filterStatus === s ? S.filterActive : {}) }}
              onClick={() => setFilterStatus(s)}>{s === 'pending' ? 'Pending' : 'All'}</button>
          ))}
        </div>
      </div>

      {actionError && <div style={S.error}>{actionError}</div>}
      {error && <div style={S.error}>{error}</div>}
      {loading && <div style={{ color: '#888', padding: 16 }}>Loading…</div>}
      {!loading && items.length === 0 && <div style={S.empty}>No change requests found.</div>}

      {!loading && items.map((cr) => (
        <div key={cr.id} style={S.card}>
          <div style={S.cardHeader}>
            <div>
              <span style={S.flagKey}>{cr.flagKey}</span>
              <span style={S.env}>{cr.environmentName}{' '}
                <span style={S.badge}>env</span>
              </span>
            </div>
            <span style={{ ...S.status, ...statusColors[cr.status] }}>{cr.status}</span>
          </div>
          <div style={S.cardBody}>
            <div style={S.infoRow}>
              <span style={S.infoLabel}>Requested by:</span>
              <code style={S.code}>{cr.requestedByUserId.substring(0, 12)}…</code>
              <span style={S.infoLabel}>on</span>
              <span>{new Date(cr.createdAt).toLocaleString()}</span>
            </div>
            <div style={{ ...S.infoRow, marginTop: 4 }}>
              <span style={S.infoLabel}>Reason:</span>
              <span>{cr.reason}</span>
            </div>
            {cr.reviewedByUserId && (
              <div style={{ ...S.infoRow, marginTop: 4 }}>
                <span style={S.infoLabel}>Reviewed by:</span>
                <code style={S.code}>{cr.reviewedByUserId.substring(0, 12)}…</code>
                {cr.reviewNote && <span style={{ color: '#666' }}>— {cr.reviewNote}</span>}
              </div>
            )}
            <details style={{ marginTop: 10 }}>
              <summary style={{ cursor: 'pointer', fontSize: 12, color: '#0D8390' }}>View proposed changes</summary>
              <pre style={S.pre}>{JSON.stringify(JSON.parse(cr.proposedChanges), null, 2)}</pre>
            </details>
          </div>
          {cr.status === 'Pending' && (role === 'Approver' || role === 'Admin') && (
            <div style={S.cardActions}>
              <input style={S.noteInput} placeholder="Optional review note…" value={reviewNote[cr.id] || ''}
                onChange={(e) => setReviewNote({ ...reviewNote, [cr.id]: e.target.value })} />
              <button style={S.approveBtn} onClick={() => handleApprove(cr.id, cr.requestedByUserId)}>✓ Approve</button>
              <button style={S.rejectBtn} onClick={() => handleReject(cr.id, cr.requestedByUserId)}>✕ Reject</button>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

const S: Record<string, React.CSSProperties> = {
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 24 },
  h2: { margin: 0, fontSize: 24 },
  filterBtn: { background: '#fff', border: '1px solid #d9d9d9', borderRadius: 5, padding: '7px 14px', cursor: 'pointer', fontSize: 13 },
  filterActive: { background: '#0D8390', color: '#fff', border: '1px solid #0D8390' },
  error: { background: '#fff0f0', border: '1px solid #ffcdd2', color: '#c62828', padding: '10px 14px', borderRadius: 6, marginBottom: 16, fontSize: 14 },
  empty: { color: '#888', textAlign: 'center', padding: 40 },
  card: { background: '#fff', borderRadius: 8, marginBottom: 16, boxShadow: '0 1px 6px rgba(0,0,0,.08)', overflow: 'hidden' },
  cardHeader: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '14px 20px', borderBottom: '1px solid #f0f0f0' },
  cardBody: { padding: '14px 20px' },
  cardActions: { display: 'flex', alignItems: 'center', gap: 10, padding: '12px 20px', background: '#f7f9fa', borderTop: '1px solid #f0f0f0' },
  flagKey: { fontFamily: 'monospace', background: '#f0f4f4', padding: '2px 8px', borderRadius: 4, fontSize: 14, marginRight: 8 },
  env: { fontSize: 13, color: '#555' },
  badge: { background: '#e6f3f5', color: '#0b4f57', borderRadius: 4, padding: '1px 6px', fontSize: 11 },
  status: { borderRadius: 4, padding: '3px 10px', fontSize: 12, fontWeight: 600 },
  infoRow: { display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, flexWrap: 'wrap' },
  infoLabel: { color: '#888', fontWeight: 500 },
  code: { background: '#f0f4f4', padding: '1px 5px', borderRadius: 3, fontFamily: 'monospace', fontSize: 12 },
  pre: { background: '#f7f9fa', border: '1px solid #e0e0e0', borderRadius: 5, padding: '10px 12px', fontSize: 12, overflowX: 'auto', marginTop: 8 },
  noteInput: { flex: 1, padding: '7px 10px', border: '1px solid #d9d9d9', borderRadius: 5, fontSize: 13 },
  approveBtn: { background: '#2e7d32', color: '#fff', border: 'none', borderRadius: 5, padding: '8px 16px', cursor: 'pointer', fontSize: 13, fontWeight: 600 },
  rejectBtn: { background: '#c62828', color: '#fff', border: 'none', borderRadius: 5, padding: '8px 16px', cursor: 'pointer', fontSize: 13, fontWeight: 600 },
};
