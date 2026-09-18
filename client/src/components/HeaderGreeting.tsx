import { Typography } from 'antd'
import { useEffect, useState } from 'react'
import type { AuthUser } from '../api'
import { formatHeaderGreeting, greetingFirstName, timeOfDayGreeting } from '../headerGreeting'
import { nextMoraleTagline } from '../morale/taglines'

export function HeaderGreeting({ user }: { user: AuthUser | null | undefined }) {
  const [now, setNow] = useState(() => new Date())
  const [tagline] = useState(() => nextMoraleTagline(user?.id))

  useEffect(() => {
    const id = window.setInterval(() => setNow(new Date()), 60_000)
    return () => window.clearInterval(id)
  }, [])

  const hello = timeOfDayGreeting(now)
  const firstName = greetingFirstName(user?.fullName)
  const lead = firstName ? `${hello}, ${firstName}` : hello
  const line = formatHeaderGreeting(user?.fullName, tagline, now)

  return (
    <Typography.Text strong className="app-header-greeting" ellipsis title={line} data-testid="header-greeting">
      <span className="app-header-hello">{lead}</span>
      {' '}
      <span className="app-header-tagline">“{tagline}”</span>
    </Typography.Text>
  )
}
