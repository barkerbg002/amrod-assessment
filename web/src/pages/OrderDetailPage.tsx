import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { orderApi, ApiError } from '../api/client';
import type { Order, OrderStatus } from '../types/api';
import { VALID_TRANSITIONS } from '../types/api';

const styles = {
  container: { padding: 24, maxWidth: 960, margin: '0 auto' } as const,
  card: { border: '1px solid #ddd', borderRadius: 8, padding: 16, marginBottom: 24 } as const,
  table: { width: '100%', borderCollapse: 'collapse' as const },
  th: { textAlign: 'left' as const, borderBottom: '2px solid #333', padding: 8 },
  td: { borderBottom: '1px solid #eee', padding: 8 },
  button: { padding: '8px 16px', cursor: 'pointer', marginRight: 8 } as const,
  error: { color: '#b00020', marginBottom: 16 } as const,
  success: { color: '#2e7d32', marginBottom: 16 } as const,
};

export function OrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [order, setOrder] = useState<Order | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [updating, setUpdating] = useState(false);

  const loadOrder = async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const data = await orderApi.getById(id);
      setOrder(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to load order');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadOrder();
  }, [id]);

  const handleStatusUpdate = async (newStatus: OrderStatus) => {
    if (!order) return;
    setUpdating(true);
    setError(null);
    setSuccess(null);
    try {
      const idempotencyKey = crypto.randomUUID();
      const updated = await orderApi.updateStatus(
        order.id,
        { status: newStatus, rowVersion: order.rowVersion },
        idempotencyKey
      );
      setOrder(updated);
      setSuccess(`Status updated to ${newStatus}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Failed to update status');
      await loadOrder();
    } finally {
      setUpdating(false);
    }
  };

  if (loading) return <div style={styles.container}>Loading...</div>;
  if (!order) return <div style={styles.container}>Order not found</div>;

  const allowedTransitions = VALID_TRANSITIONS[order.status];

  return (
    <div style={styles.container}>
      <Link to="/orders">&larr; Back to Orders</Link>
      <h1>Order Detail</h1>

      {error && <div style={styles.error}>{error}</div>}
      {success && <div style={styles.success}>{success}</div>}

      <div style={styles.card}>
        <p>
          <strong>ID:</strong> {order.id}
        </p>
        <p>
          <strong>Customer:</strong> {order.customerId}
        </p>
        <p>
          <strong>Status:</strong> {order.status}
        </p>
        <p>
          <strong>Total:</strong> {order.currencyCode} {order.totalAmount.toFixed(2)}
        </p>
        <p>
          <strong>Created:</strong> {new Date(order.createdAt).toLocaleString()}
        </p>
      </div>

      <h2>Line Items</h2>
      <table style={styles.table}>
        <thead>
          <tr>
            <th style={styles.th}>SKU</th>
            <th style={styles.th}>Quantity</th>
            <th style={styles.th}>Unit Price</th>
            <th style={styles.th}>Line Total</th>
          </tr>
        </thead>
        <tbody>
          {order.lineItems.map((li) => (
            <tr key={li.id}>
              <td style={styles.td}>{li.productSku}</td>
              <td style={styles.td}>{li.quantity}</td>
              <td style={styles.td}>{li.unitPrice.toFixed(2)}</td>
              <td style={styles.td}>{li.lineTotal.toFixed(2)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {allowedTransitions.length > 0 && (
        <div style={{ marginTop: 24 }}>
          <h3>Update Status</h3>
          {allowedTransitions.map((s) => (
            <button
              key={s}
              style={styles.button}
              disabled={updating}
              onClick={() => handleStatusUpdate(s)}
            >
              Mark as {s}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
