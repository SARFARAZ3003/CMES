import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import Login from './pages/Login'
import ProductionReport from './pages/ProductionReport'
import ModelTracking from './pages/ModelTracking'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/"                   element={<Login />} />
        <Route path="/production-report"  element={<ProductionReport />} />
        <Route path="/model-tracking"     element={<ModelTracking />} />
        {/* All unknown paths go back to login */}
        <Route path="*" element={<Navigate to="/" />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
