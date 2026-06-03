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
  { hour: '6', oldLine: 28, newLine: 25 },
  { hour: '7', oldLine: 26, newLine: 24 },
  { hour: '8', oldLine: 28, newLine: 21 },
  { hour: '9', oldLine: 21, newLine: 18 },
  { hour: '10', oldLine: 18, newLine: 16 },
  { hour: '11', oldLine: 22, newLine: 19 },
  { hour: '12', oldLine: 15, newLine: 8 },
  { hour: '13', oldLine: 9,  newLine: 4  },
  { hour: '14', oldLine: 7,  newLine: 0  },
  { hour: '15', oldLine: 0,  newLine: 0  },
  { hour: '16', oldLine: 0,  newLine: 0  },
  { hour: '17', oldLine: 0,  newLine: 0  },
]

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
  { month: 'Jan', production: 11040 },
  { month: 'Feb', production: 12826 },
  { month: 'Mar', production: 12617 },
  { month: 'Apr', production: 12679 },
  { month: 'May', production: 7887  },
  { month: 'Jun', production: 0 },
  { month: 'Jul', production: 0 },
  { month: 'Aug', production: 0 },
  { month: 'Sep', production: 0 },
  { month: 'Oct', production: 0 },
  { month: 'Nov', production: 0 },
  { month: 'Dec', production: 0 },
]