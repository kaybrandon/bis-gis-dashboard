export function PoweredByFooter({ onDark = false }: { onDark?: boolean }) {
  return (
    <p className={onDark ? 'powered-by powered-by-on-dark' : 'powered-by'}>
      Powered By:{' '}
      <a href="https://www.bisconsultants.com" target="_blank" rel="noopener noreferrer">
        BIS Consultants
      </a>
    </p>
  )
}
