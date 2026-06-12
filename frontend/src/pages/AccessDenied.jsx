import './AccessDenied.css'

// Jab detected Windows user CMES_USERS mein active nahi (ya mila hi nahi).
export default function AccessDenied({ info }) {
  const username = info?.username
  return (
    <div className="ad-root">
      <div className="ad-card">
        <div className="ad-logo">TCL <span>CMES</span></div>
        <div className="ad-icon">🔒</div>
        <h1 className="ad-title">Access Denied</h1>
        <p className="ad-msg">
          {info?.message || 'You are not authorized to access CMES.'}
        </p>
        {username && (
          <div className="ad-user">
            Windows user: <strong>{username}</strong>
          </div>
        )}
        <p className="ad-hint">
          Please contact the CMES administrator to request access.
        </p>
      </div>
    </div>
  )
}
