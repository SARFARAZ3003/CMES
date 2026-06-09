import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import ProductionReport from './pages/ProductionReport'
import WipReport from './pages/WipReport'
import ModelTracking from './pages/ModelTracking'
import Inventory from './pages/Inventory'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Login />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/production-report" element={<ProductionReport />} />
        <Route path="/wip-report" element={<WipReport />} />
        <Route path="/model-tracking" element={<ModelTracking />} />
        <Route path="/inventory" element={<Inventory />} />
        <Route path="*" element={<Navigate to="/" />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
