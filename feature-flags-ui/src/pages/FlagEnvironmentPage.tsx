import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import * as api from '../api/apiClient';
import { useSelector } from 'react-redux';
import { RootState } from '../app/store';

type Rule = Partial<api.TargetingRule> & { _tempId?: number };

export default function FlagEnvironmentPage() {
  const { flagId, envId } = useParams<{ flagId: string; envId: string }>();
  const { role } = useSelector((s: RootState) => s.auth);
  const [fev, setFev] = useState<api.FlagEnvironmentValue | null>(null);
  const [flag, setFlag] = useState<api.Flag | null>(null);
  const [history, setHistory] = useState<api.FlagVersion[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [isEnabled, setIsEnabled] = useState(false);
  const [value, setValue] = useState('');
  const [rules, setRules] = useState<Rule[]>([]);
  const [reason, setReason] = useState('');
  const [showHistory, setShowHistory] = useState(false);

  const fId = parseInt(flagId!);
  const eId = parseInt(envId!);

  useEffect(() => {
    loadData();
  }, [flagId, envId]);

  async function loadData() {
    setLoading(true);
    try {
      const [fevRes, flagRes, histRes] = await Promise.all([
        api.getFlagEnvValue(fId, eId),
        api.getFlag(fId),
        api.getHistory(fId, eId),
      ]);
      setFev(fevRes.data);
      setFlag(flagRes.data);
      setHistory(histRes.data);
      setIsEnabled(fevRes.data.isEnabled);
      setValue(fevRes.data.value || '');
      setRules(fevRes.data.rules.map((r, i) => ({ ...r, _tempId: i })));
    } catch { setError('Failed to load flag data.'); }
    finally { setLoading(false); }
  }

  const handleSave = async () => {
    if (!fev) return;
    if (!reason.trim()) { setError('Please enter a reason for this change.'); return; }
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      const res = await api.updateFlagEnvValue(fId, eId, {
        isEnabled,
        value: value || undefined,
        rules: rules.map(({ type, percentage, attributeKey, operator, attributeValue, order, serveValue }) =>
          ({ type, percentage, attributeKey, operator, attributeValue, order: order || 1, serveValue })),
        reason,
        rowVersion: fev.rowVersion,
      });
      const data = res.data as any;
      if (data?.changeRequestId) {
        setSuccess(`Change request #${data.changeRequestId} submitted for approval.`);
      } else {
        setSuccess('Changes saved.');
        await loadData();
      }
      setReason('');
    } catch (err: any) {
      if (err.response?.status === 409)
        setError('Conflict: someone else changed this flag. Please reload.');
      else
        setError(err.response?.data?.error || 'Save failed.');
    }
    setSaving(false);
  };

  const handleRollback = async (versionId: number, version: number) => {
    if (!window.confirm(`Roll back to version ${version}? This creates a new version restoring that state.`)) return;
    try {
      await api.rollback(fId, eId, versionId);
      setSuccess(`Rolled back to version ${version}.`);
      await loadData();
    } catch (err: any) { setError(err.response?.data?.error || 'Rollback failed.'); }
  };

  const addRule = () => {
    setRules([...rules, { type: 'Attribute', order: rules.length + 1, _tempId: Date.now() }]);
  };

  const removeRule = (tempId: number) => setRules(rules.filter((r) => r._tempId !== tempId));

  const updateRule = (tempId: number, patch: Partial<Rule>) =>
    setRules(rules.map((r) => r._tempId === tempId ? { ...r, ...patch } : r));

  if (loading) return <div style={{ padding: 24, color: '#888' }}>Loading…</div>;
  if (!fev || !flag) return <div style={{ padding: 24, color: '#c62828' }}>Flag not found.</div>;

  const env = fev;
  const isProtected = /* will check via flag env */ true;

  return (
    <div style={{ maxWidth: 800 }}>
      <div style={S.header}>
        <div>
          <h2 style={S.h2}><code>{flag.key}</code> · <span style={S.envBadge}>{env.environmentName}</span></h2>
          <p style={S.sub}>{flag.name} · {flag.type}</p>
        </div>
      </div>

      {error && <div style={S.error}>{error}</div>}
      {success && <div style={S.success}>{success}</div>}

      {/* Toggle */}
      <div style={S.section}>
        <h3 style={S.h3}>Flag State</h3>
        <label style={S.toggle}>
          <input type="checkbox" checked={isEnabled} onChange={(e) => setIsEnabled(e.target.checked)} style={{ display: 'none' }} />
          <div style={{ ...S.toggleTrack, background: isEnabled ? '#0D8390' : '#ccc' }}>
            <div style={{ ...S.toggleThumb, transform: isEnabled ? 'translateX(22px)' : 'translateX(2px)' }} />
          </div>
          <span style={{ marginLeft: 12 }}>{isEnabled ? 'Enabled' : 'Disabled'}</span>
        </label>
      </div>

      {/* Default Value */}
      <div style={S.section}>
        <h3 style={S.h3}>Default Value</h3>
        <input style={S.input} value={value} onChange={(e) => setValue(e.target.value)} placeholder={flag.type === 'Boolean' ? 'true or false' : 'Enter value'} />
      </div>

      {/* Targeting Rules */}
      <div style={S.section}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
          <h3 style={{ ...S.h3, margin: 0 }}>Targeting Rules</h3>
          <button style={S.addBtn} onClick={addRule}>+ Add Rule</button>
        </div>
        {rules.length === 0 && <p style={{ color: '#888', fontSize: 13 }}>No rules — flag evaluates to default value.</p>}
        {rules.map((rule, idx) => (
          <div key={rule._tempId} style={S.ruleCard}>
            <div style={S.ruleHeader}>
              <span style={S.ruleNum}>Rule {idx + 1}</span>
              <button style={S.removeBtn} onClick={() => removeRule(rule._tempId!)}>✕</button>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
              <div>
                <label style={S.label}>Type</label>
                <select style={S.select} value={rule.type} onChange={(e) => updateRule(rule._tempId!, { type: e.target.value as any })}>
                  <option value="Percentage">Percentage Rollout</option>
                  <option value="Attribute">Attribute</option>
                </select>
              </div>
              <div>
                <label style={S.label}>Serve Value</label>
                <input style={S.input} value={rule.serveValue || ''} onChange={(e) => updateRule(rule._tempId!, { serveValue: e.target.value })} placeholder="true" />
              </div>
            </div>
            {rule.type === 'Percentage' && (
              <div style={{ marginTop: 10 }}>
                <label style={S.label}>Rollout % (0–100)</label>
                <input type="number" min={0} max={100} style={S.input} value={rule.percentage ?? ''} onChange={(e) => updateRule(rule._tempId!, { percentage: Number(e.target.value) })} />
              </div>
            )}
            {rule.type === 'Attribute' && (
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 10, marginTop: 10 }}>
                <div>
                  <label style={S.label}>Attribute Key</label>
                  <input style={S.input} value={rule.attributeKey || ''} onChange={(e) => updateRule(rule._tempId!, { attributeKey: e.target.value })} placeholder="country" />
                </div>
                <div>
                  <label style={S.label}>Operator</label>
                  <select style={S.select} value={rule.operator || 'Equals'} onChange={(e) => updateRule(rule._tempId!, { operator: e.target.value as any })}>
                    <option value="Equals">Equals</option>
                    <option value="NotEquals">Not Equals</option>
                    <option value="InList">In List (JSON)</option>
                  </select>
                </div>
                <div>
                  <label style={S.label}>Value</label>
                  <input style={S.input} value={rule.attributeValue || ''} onChange={(e) => updateRule(rule._tempId!, { attributeValue: e.target.value })} placeholder={rule.operator === 'InList' ? '["a","b"]' : 'US'} />
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Reason + Save */}
      <div style={S.section}>
        <label style={S.label}>Reason for change <span style={{ color: '#c62828' }}>*</span></label>
        <input style={S.input} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Why are you making this change?" />
        <button style={{ ...S.saveBtn, marginTop: 12, opacity: saving ? 0.7 : 1 }} onClick={handleSave} disabled={saving}>
          {saving ? 'Saving…' : 'Save Changes'}
        </button>
      </div>

      {/* History */}
      <div style={S.section}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <h3 style={S.h3}>Change History</h3>
          <button style={S.addBtn} onClick={() => setShowHistory(!showHistory)}>{showHistory ? 'Hide' : 'Show'}</button>
        </div>
        {showHistory && (
          <table style={S.table}>
            <thead>
              <tr>{['Ver', 'Changed', 'By', 'Reason', 'Rollback?', 'Action'].map(h => <th key={h} style={S.th}>{h}</th>)}</tr>
            </thead>
            <tbody>
              {history.map((v) => (
                <tr key={v.id}>
                  <td style={S.td}>v{v.version}</td>
                  <td style={S.td}>{new Date(v.changedAt).toLocaleString()}</td>
                  <td style={S.td}><code style={S.code}>{v.changedByUserId.substring(0, 8)}…</code></td>
                  <td style={S.td}>{v.reason}</td>
                  <td style={S.td}>{v.isRollback ? '↩ Yes' : '—'}</td>
                  <td style={S.td}>
                    {role === 'Admin' && history[0]?.id !== v.id && (
                      <button style={S.rollbackBtn} onClick={() => handleRollback(v.id, v.version)}>↩ Restore</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

const S: Record<string, React.CSSProperties> = {
  header: { marginBottom: 24 },
  h2: { margin: 0, fontSize: 22 },
  sub: { margin: '4px 0 0', color: '#666', fontSize: 14 },
  envBadge: { background: '#e6f3f5', color: '#0b4f57', padding: '2px 10px', borderRadius: 20, fontSize: 14, fontWeight: 500 },
  h3: { fontSize: 15, fontWeight: 600, color: '#333', marginBottom: 12 },
  section: { background: '#fff', borderRadius: 8, padding: 20, marginBottom: 16, boxShadow: '0 1px 4px rgba(0,0,0,.07)' },
  toggle: { display: 'flex', alignItems: 'center', cursor: 'pointer', fontSize: 15 },
  toggleTrack: { width: 46, height: 24, borderRadius: 12, position: 'relative', transition: 'background .2s' },
  toggleThumb: { width: 20, height: 20, background: '#fff', borderRadius: 10, position: 'absolute', top: 2, transition: 'transform .2s' },
  input: { width: '100%', padding: '9px 12px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14 },
  select: { width: '100%', padding: '9px 12px', border: '1px solid #d9d9d9', borderRadius: 6, fontSize: 14 },
  label: { display: 'block', marginBottom: 5, fontSize: 12, fontWeight: 500, color: '#555' },
  addBtn: { background: 'transparent', border: '1px solid #0D8390', color: '#0D8390', borderRadius: 5, padding: '5px 12px', cursor: 'pointer', fontSize: 13 },
  removeBtn: { background: 'transparent', border: 'none', color: '#888', cursor: 'pointer', fontSize: 14 },
  rollbackBtn: { background: '#ff9800', color: '#fff', border: 'none', borderRadius: 4, padding: '4px 10px', cursor: 'pointer', fontSize: 12 },
  ruleCard: { border: '1px solid #e0e0e0', borderRadius: 6, padding: 14, marginBottom: 10 },
  ruleHeader: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 },
  ruleNum: { fontSize: 12, fontWeight: 600, color: '#0D8390' },
  saveBtn: { background: '#0D8390', color: '#fff', border: 'none', borderRadius: 6, padding: '11px 24px', cursor: 'pointer', fontSize: 15, fontWeight: 600 },
  error: { background: '#fff0f0', border: '1px solid #ffcdd2', color: '#c62828', padding: '10px 14px', borderRadius: 6, marginBottom: 16, fontSize: 14 },
  success: { background: '#f0fff4', border: '1px solid #c8e6c9', color: '#2e7d32', padding: '10px 14px', borderRadius: 6, marginBottom: 16, fontSize: 14 },
  table: { width: '100%', borderCollapse: 'collapse' },
  th: { background: '#f7f9fa', padding: '8px 12px', textAlign: 'left', fontSize: 12, fontWeight: 600, color: '#555', borderBottom: '1px solid #eee' },
  td: { padding: '10px 12px', fontSize: 13, borderBottom: '1px solid #f0f0f0' },
  code: { background: '#f0f4f4', padding: '1px 5px', borderRadius: 3, fontFamily: 'monospace', fontSize: 12 },
};
