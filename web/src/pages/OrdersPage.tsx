import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { customerApi, orderApi, ApiError } from '../api/client';
import type {
  CreateOrderLineItemRequest,
  CreateOrderRequest,
  Customer,
  Order,
  OrderStatus,
} from '../types/api';
import { CURRENCIES } from '../types/api';

const styles = {
  container: { padding: 24, maxWidth: 1100, margin: '0 auto' } as const,
  filters: { display: 'flex', gap: 12, marginBottom: 16, flexWrap: 'wrap' as const },
  input: { padding: 8, fontSize: 14 } as const,
  button: { padding: '8px 16px', cursor: 'pointer' } as const,
  table: { width: '100%', borderCollapse: 'collapse' as const },
  th: { textAlign: 'left' as const, borderBottom: '2px solid #333', padding: 8 },
  td: { borderBottom: '1px solid #eee', padding: 8 },
  form: { display: 'grid', gap: 12, marginBottom: 32, padding: 16, border: '1px solid #ddd', borderRadius: 8 } as const,
  error: { color: '#b00020', marginBottom: 16 } as const,
  pagination: { display: 'flex', gap: 12, alignItems: 'center', marginTop: 16 } as const,
  lineItem: { display: 'grid', gridTemplateColumns: '2fr 1fr 1fr auto', gap: 8, alignItems: 'center' } as const,
};

const STATUSES: OrderStatus[] = ['Pending', 'Paid', 'Fulfilled', 'Cancelled'];

export function OrdersPage() {
  const [orders, setOrders] = useState<Order[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [customerId, setCustomerId] = useState('');
  const [status, setStatus] = useState<OrderStatus | ''>('');
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState<CreateOrderRequest>({
    customerId: '',
    currencyCode: 'ZAR',
    lineItems: [{ productSku: '', quantity: 1, unitPrice: 0 }],
  });

  const loadOrders = async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await orderApi.search(
        customerId || undefined,
        status || undefined,
        page,
        10
      );
      setOrders(result.items);
      setTotalPages(result.totalPages);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to load orders');
    } finally {
      setLoading(false);
    }
  };

  const loadCustomers = async () => {
    try {
      const result = await customerApi.search('', 1, 100);
      setCustomers(result.items);
    } catch {
      /* ignore */
    }
  };

  useEffect(() => {
    loadCustomers();
  }, []);

  useEffect(() => {
    loadOrders();
  }, [page, customerId, status]);

  const addLineItem = () => {
    setForm({
      ...form,
      lineItems: [...form.lineItems, { productSku: '', quantity: 1, unitPrice: 0 }],
    });
  };

  const updateLineItem = (index: number, field: keyof CreateOrderLineItemRequest, value: string | number) => {
    const items = [...form.lineItems];
    items[index] = { ...items[index], [field]: value };
    setForm({ ...form, lineItems: items });
  };

  const removeLineItem = (index: number) => {
    setForm({ ...form, lineItems: form.lineItems.filter((_, i) => i !== index) });
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await orderApi.create(form);
      setForm({
        customerId: form.customerId,
        currencyCode: 'ZAR',
        lineItems: [{ productSku: '', quantity: 1, unitPrice: 0 }],
      });
      setPage(1);
      await loadOrders();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to create order');
    }
  };

  return (
    <div style={styles.container}>
      <h1>Orders</h1>

      <form style={styles.form} onSubmit={handleCreate}>
        <h3>Create Order</h3>
        <select
          style={styles.input}
          value={form.customerId}
          onChange={(e) => setForm({ ...form, customerId: e.target.value })}
          required
        >
          <option value="">Select customer...</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name} ({c.countryCode})
            </option>
          ))}
        </select>
        <select
          style={styles.input}
          value={form.currencyCode}
          onChange={(e) => setForm({ ...form, currencyCode: e.target.value })}
        >
          {CURRENCIES.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>

        <h4>Line Items</h4>
        {form.lineItems.map((item, i) => (
          <div key={i} style={styles.lineItem}>
            <input
              style={styles.input}
              placeholder="SKU"
              value={item.productSku}
              onChange={(e) => updateLineItem(i, 'productSku', e.target.value)}
              required
            />
            <input
              style={styles.input}
              type="number"
              min={1}
              placeholder="Qty"
              value={item.quantity}
              onChange={(e) => updateLineItem(i, 'quantity', parseInt(e.target.value) || 1)}
              required
            />
            <input
              style={styles.input}
              type="number"
              min={0}
              step={0.01}
              placeholder="Unit Price"
              value={item.unitPrice}
              onChange={(e) => updateLineItem(i, 'unitPrice', parseFloat(e.target.value) || 0)}
              required
            />
            <button type="button" style={styles.button} onClick={() => removeLineItem(i)}>
              Remove
            </button>
          </div>
        ))}
        <button type="button" style={styles.button} onClick={addLineItem}>
          Add Line Item
        </button>
        <button style={styles.button} type="submit">
          Create Order
        </button>
      </form>

      <div style={styles.filters}>
        <select
          style={styles.input}
          value={customerId}
          onChange={(e) => {
            setCustomerId(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All customers</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <select
          style={styles.input}
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as OrderStatus | '');
            setPage(1);
          }}
        >
          <option value="">All statuses</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </div>

      {error && <div style={styles.error}>{error}</div>}
      {loading && <p>Loading...</p>}

      <table style={styles.table}>
        <thead>
          <tr>
            <th style={styles.th}>ID</th>
            <th style={styles.th}>Customer</th>
            <th style={styles.th}>Status</th>
            <th style={styles.th}>Total</th>
            <th style={styles.th}>Created</th>
            <th style={styles.th}></th>
          </tr>
        </thead>
        <tbody>
          {orders.map((o) => (
            <tr key={o.id}>
              <td style={styles.td}>{o.id.slice(0, 8)}...</td>
              <td style={styles.td}>{o.customerId.slice(0, 8)}...</td>
              <td style={styles.td}>{o.status}</td>
              <td style={styles.td}>
                {o.currencyCode} {o.totalAmount.toFixed(2)}
              </td>
              <td style={styles.td}>{new Date(o.createdAt).toLocaleString()}</td>
              <td style={styles.td}>
                <Link to={`/orders/${o.id}`}>View</Link>
              </td>
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
