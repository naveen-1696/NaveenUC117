import { createAsyncThunk, createSlice } from '@reduxjs/toolkit';
import * as api from '../../api/apiClient';

interface CRState {
  items: api.ChangeRequest[];
  loading: boolean;
  error: string | null;
}

const initialState: CRState = { items: [], loading: false, error: null };

export const fetchChangeRequests = createAsyncThunk('changeRequests/fetch', async (status?: string) => {
  const { data } = await api.getChangeRequests(status);
  return data;
});

export const approveRequest = createAsyncThunk(
  'changeRequests/approve',
  async ({ id, reviewNote }: { id: number; reviewNote?: string }, { rejectWithValue }) => {
    try {
      await api.approveChangeRequest(id, reviewNote);
      return id;
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.error || 'Approve failed');
    }
  }
);

export const rejectRequest = createAsyncThunk(
  'changeRequests/reject',
  async ({ id, reviewNote }: { id: number; reviewNote?: string }, { rejectWithValue }) => {
    try {
      await api.rejectChangeRequest(id, reviewNote);
      return id;
    } catch (err: any) {
      return rejectWithValue(err.response?.data?.error || 'Reject failed');
    }
  }
);

const slice = createSlice({
  name: 'changeRequests',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchChangeRequests.pending, (s) => { s.loading = true; s.error = null; })
      .addCase(fetchChangeRequests.fulfilled, (s, { payload }) => { s.loading = false; s.items = payload; })
      .addCase(fetchChangeRequests.rejected, (s, { error }) => { s.loading = false; s.error = error.message ?? 'Error'; })
      .addCase(approveRequest.fulfilled, (s, { payload }) => {
        const cr = s.items.find((x) => x.id === payload);
        if (cr) cr.status = 'Approved';
      })
      .addCase(rejectRequest.fulfilled, (s, { payload }) => {
        const cr = s.items.find((x) => x.id === payload);
        if (cr) cr.status = 'Rejected';
      });
  },
});

export default slice.reducer;
