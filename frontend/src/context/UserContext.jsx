import { createContext, useContext, useState } from 'react'
import { resolveUserFromComputer } from '../data/computerRegistry'

const UserContext = createContext(null)

/**
 * Wraps the whole app. Resolves the current user once on mount
 * and makes it available anywhere via useUser().
 */
export function UserProvider({ children }) {
  // Resolve once — no re-resolution needed unless hostname changes (it won't)
  const [user] = useState(() => resolveUserFromComputer())

  return (
    <UserContext.Provider value={user}>
      {children}
    </UserContext.Provider>
  )
}

/** Hook: const user = useUser() */
export function useUser() {
  const ctx = useContext(UserContext)
  if (!ctx) throw new Error('useUser must be used inside <UserProvider>')
  return ctx
}
