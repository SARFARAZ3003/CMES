import axios from 'axios'

// Backend ASP.NET localhost:5000 pe chalta hain.
// withCredentials: Windows Integrated Auth ke liye browser credentials bheje.
const api = axios.create({
  baseURL: 'http://localhost:5000/api',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

export default api
