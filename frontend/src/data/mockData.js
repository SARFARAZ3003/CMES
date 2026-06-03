export const currentUser = {
  code: 'OD741',
  name: 'Sarfaraz Ahmed',
  department: 'Production',
  shift: 'A',
}

export const dashboardKPIs = {
  productionToday: 101,
  wipCount: 263,
  fesCount: 98,
  testOK: 111,
  dispatched: 0,
  activeModels: 18,
}

export const shiftData = {
  A: {
    oldLine: 97,
    newLine: 102,
    testCycle: 137,
    fes: 98,
    dispatched: 0,
    testOK: 111,
  },
  B: { oldLine: 0, newLine: 0, testCycle: 0, fes: 0, dispatched: 0, testOK: 0 },
  C: { oldLine: 0, newLine: 0, testCycle: 0, fes: 0, dispatched: 0, testOK: 0 },
}

export const hourlyData = [
  { hour: '6', oldLine: 28, newLine: 25, testCycle: 30, fes: 27, dispatched: 25, testOK: 26 },
  { hour: '7', oldLine: 26, newLine: 24, testCycle: 28, fes: 25, dispatched: 23, testOK: 24 },
  { hour: '8', oldLine: 28, newLine: 21, testCycle: 26, fes: 24, dispatched: 22, testOK: 23 },
  { hour: '9', oldLine: 21, newLine: 18, testCycle: 22, fes: 20, dispatched: 18, testOK: 19 },
  { hour: '10', oldLine: 18, newLine: 16, testCycle: 19, fes: 17, dispatched: 15, testOK: 16 },
  { hour: '11', oldLine: 22, newLine: 19, testCycle: 24, fes: 21, dispatched: 19, testOK: 20 },
  { hour: '12', oldLine: 15, newLine: 8, testCycle: 12, fes: 10, dispatched: 8, testOK: 9 },
  { hour: '13', oldLine: 9,  newLine: 4, testCycle: 7, fes: 5, dispatched: 4, testOK: 5 },
  { hour: '14', oldLine: 7,  newLine: 0, testCycle: 4, fes: 2, dispatched: 0, testOK: 1 },
  { hour: '15', oldLine: 0,  newLine: 0, testCycle: 0, fes: 0, dispatched: 0, testOK: 0 },
  { hour: '16', oldLine: 0,  newLine: 0, testCycle: 0, fes: 0, dispatched: 0, testOK: 0 },
  { hour: '17', oldLine: 0,  newLine: 0, testCycle: 0, fes: 0, dispatched: 0, testOK: 0 },
]

// Daily production data (for each day of the month)
export const dailyData = Array.from({ length: 31 }, (_, i) => ({
  day: i + 1,
  oldLine: Math.floor(Math.random() * 200) + 300,
  newLine: Math.floor(Math.random() * 200) + 300,
  testCycle: Math.floor(Math.random() * 150) + 250,
  fes: Math.floor(Math.random() * 150) + 250,
  dispatched: Math.floor(Math.random() * 150) + 200,
  testOK: Math.floor(Math.random() * 150) + 200,
}))

export const wipLocations = [
  { location: 'Quality Dock', count: 91 },
  { location: 'Paint Line',   count: 48 },
  { location: 'Test Cell',    count: 41 },
  { location: 'New Line',     count: 22 },
  { location: 'PE',           count: 15 },
  { location: 'Lineset Line', count: 15 },
  { location: 'EQA Audit',    count: 13 },
  { location: 'Old Line',     count: 5  },
  { location: 'Others',       count: 13 },
]

export const monthlyData = [
  { month: 'JAN', oldLine: 11040, newLine: 11945, testCall: 66, fes: 0 },
  { month: 'FEB', oldLine: 13826, newLine: 12522, testCall: 10427, fes: 10667 },
  { month: 'MAR', oldLine: 12617, newLine: 0, testCall: 11244, fes: 12617 },
  { month: 'APR', oldLine: 12679, newLine: 12153, testCall: 10136, fes: 12767 },
  { month: 'MAY', oldLine: 7887, newLine: 8428, testCall: 7011, fes: 8122 },
  { month: 'JUN', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
  { month: 'JUL', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
  { month: 'AUG', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
  { month: 'SEP', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
  { month: 'OCT', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
  { month: 'NOV', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
  { month: 'DEC', oldLine: 0, newLine: 0, testCall: 0, fes: 0 },
]