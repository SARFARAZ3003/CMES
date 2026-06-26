import axios from 'axios'

// Dev (npm run dev): backend alag :5000 pe -> full URL.
// Prod (npm run build, IIS pe wwwroot se serve): same-origin -> relative '/api' (koi hardcoded host nahi).
// withCredentials: Windows Integrated Auth ke liye browser credentials bheje.
const api = axios.create({
  baseURL: import.meta.env.DEV ? 'http://localhost:5000/api' : '/api',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

export default api
