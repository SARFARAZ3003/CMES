import axios from 'axios'

// Backend ASP.NET localhost:5000 pe chalta hain
const api = axios.create({
  baseURL: 'http://localhost:5000/api',
  headers: { 'Content-Type': 'application/json' },
})

export default api
