import { BrowserRouter, Link, Route, Routes } from 'react-router-dom';
import { ErrorBoundary } from './components/ErrorBoundary';
import { CustomersPage } from './pages/CustomersPage';
import { OrdersPage } from './pages/OrdersPage';
import { OrderDetailPage } from './pages/OrderDetailPage';

const navStyle = {
  display: 'flex',
  gap: 24,
  padding: '12px 24px',
  background: '#1a1a2e',
  color: '#fff',
} as const;

const linkStyle = { color: '#eee', textDecoration: 'none' } as const;

export function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
        <nav style={navStyle}>
          <strong style={{ marginRight: 'auto' }}>SadcOMS</strong>
          <Link to="/customers" style={linkStyle}>
            Customers
          </Link>
          <Link to="/orders" style={linkStyle}>
            Orders
          </Link>
        </nav>
        <Routes>
          <Route path="/" element={<CustomersPage />} />
          <Route path="/customers" element={<CustomersPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/orders/:id" element={<OrderDetailPage />} />
        </Routes>
      </BrowserRouter>
    </ErrorBoundary>
  );
}
