import { STATUS_LABEL } from '../lib/format'

export default function StatusBadge({ status }) {
  return <span className={`badge badge-${status}`}>{STATUS_LABEL[status]}</span>
}
