import { Card, Typography } from 'antd'
import { useEffect, useState } from 'react'
import type { CompanyContact } from '../api'
import { publicApi } from '../api'

export function websiteHref(website: string) {
  const trimmed = website.trim()
  if (!trimmed) return ''
  if (/^https?:\/\//i.test(trimmed)) return trimmed
  return `https://${trimmed}`
}

export function CompanyContactBlock({ className }: { className?: string }) {
  const [contact, setContact] = useState<CompanyContact | null>(null)

  useEffect(() => {
    publicApi.company().then(setContact).catch(() => setContact(null))
  }, [])

  if (!contact) return null

  const phone = contact.phone?.trim()
  const email = contact.email?.trim()
  const address = contact.address?.trim()
  const website = contact.website?.trim()
  if (!phone && !email && !address && !website) return null

  return (
    <Card
      className={`compact-card company-contact ${className ?? ''}`.trim()}
      title={contact.name || 'BIS Consultants'}
    >
      <div className="company-contact-lines">
        {phone && (
          <Typography.Paragraph className="company-contact-line">
            <span className="company-contact-label">Phone</span>
            <a href={`tel:${phone.replace(/[^\d+]/g, '')}`}>{phone}</a>
          </Typography.Paragraph>
        )}
        {email && (
          <Typography.Paragraph className="company-contact-line">
            <span className="company-contact-label">Email</span>
            <a href={`mailto:${email}`}>{email}</a>
          </Typography.Paragraph>
        )}
        {address && (
          <Typography.Paragraph className="company-contact-line">
            <span className="company-contact-label">Address</span>
            <span>{address}</span>
          </Typography.Paragraph>
        )}
        {website && (
          <Typography.Paragraph className="company-contact-line">
            <span className="company-contact-label">Website</span>
            <a href={websiteHref(website)} target="_blank" rel="noreferrer">{website}</a>
          </Typography.Paragraph>
        )}
      </div>
    </Card>
  )
}
