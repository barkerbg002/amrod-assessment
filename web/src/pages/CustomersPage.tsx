import { useEffect, useState } from 'react';
import { customerApi, ApiError } from '../api/client';
import type { Customer, CreateCustomerRequest } from '../types/api';
import { SADC_COUNTRIES } from '../types/api';

const styles = {
  container: { padding: 24, maxWidth: 960, margin: '0 auto' } as const,
  form: { display: 'grid', gap: 12, marginBottom: 32, padding: 16, border: '1px solid #ddd', borderRadius: 8 } as const,
  input: { padding: 8, fontSize: 14 } as const,
  button: { padding: '8px 16px', cursor: 'pointer' } as const,
  table: { width: '100%', borderCollapse: 'collapse' as const },
  th: { textAlign: 'left' as const, borderBottom: '2px solid #333', padding: 8 },
  td: { borderBottom: '1px solid #eee', padding: 8 },
  error: { color: '#b00020', marginBottom: 16 } as const,
  pagination: { display: 'flex', gap: 12, alignItems: 'center', marginTop: 16 } as const,
};

export function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<CreateCustomerRequest>({
    name: '',
    email: '',
    countryCode: 'ZA',
  });

  const loadCustomers = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await customerApi.search(search, page, 10);
      setCustomers(result.items);
      setTotalPages(result.totalPages);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to load customers');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadCustomers();
  }, [page, search]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await customerApi.create(form);
      setForm({ name: '', email: '', countryCode: 'ZA' });
      setPage(1);
      await loadCustomers();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to create customer');
    }
  };

  return (
    <div style={styles.container}>
      <h1>Customers</h1>

      <form style={styles.form} onSubmit={handleCreate}>
        <h3>Create Customer</h3>
        <input
          style={styles.input}
          placeholder="Name"
          value={form.name}
          onChange={(e) => setForm({ ...form, name: e.target.value })}
          required
        />
        <input
          style={styles.input}
          type="email"
          placeholder="Email"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
          required
        />
        <select
          style={styles.input}
          value={form.countryCode}
          onChange={(e) => setForm({ ...form, countryCode: e.target.value })}
        >
          {SADC_COUNTRIES.map((c) => (
            <option key={c.code} value={c.code}>
              {c.name} ({c.code})
            </option>
          ))}
        </select>
        <button style={styles.button} type="submit">
          Create
        </button>
      </form>

      <input
        style={{ ...styles.input, width: '100%', marginBottom: 16 }}
        placeholder="Search by name or email..."
        value={search}
        onChange={(e) => {
          setSearch(e.target.value);
          setPage(1);
        }}
      />

      {error && <div style={styles.error}>{error}</div>}
      {loading && <p>Loading...</p>}

      <table style={styles.table}>
        <thead>
          <tr>
            <th style={styles.th}>Name</th>
            <th style={styles.th}>Email</th>
            <th style={styles.th}>Country</th>
            <th style={styles.th}>Created</th>
          </tr>
        </thead>
        <tbody>
          {customers.map((c) => (
            <tr key={c.id}>
              <td style={styles.td}>{c.name}</td>
              <td style={styles.td}>{c.email}</td>
              <td style={styles.td}>{c.countryCode}</td>
              <td style={styles.td}>{new Date(c.createdAt).toLocaleString()}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div style={styles.pagination}>
        <button style={styles.button} disabled={page <= 1} onClick={() => setPage(page - 1)}>
          Previous
        </button>
        <span>
          Page {page} of {totalPages}
        </span>
        <button
          style={styles.button}
          disabled={page >= totalPages}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  );
}
