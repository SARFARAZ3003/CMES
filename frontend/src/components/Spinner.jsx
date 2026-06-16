import './Spinner.css'

// Reusable loading spinner. `full` = poori screen center, warna inline.
export default function Spinner({ label, full = false, small = false }) {
  return (
    <div className={`spin-wrap ${full ? 'full' : ''}`}>
      <div className={`spin-ring ${small ? 'small' : ''}`} />
      {label && <div className="spin-label">{label}</div>}
    </div>
  )
}
