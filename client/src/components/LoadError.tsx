import { Alert, Button } from 'antd'

type Props = {
  message: string
  onRetry?: () => void
}

export function LoadError({ message, onRetry }: Props) {
  return (
    <Alert
      type="error"
      showIcon
      message={message}
      action={
        onRetry ? (
          <Button size="small" onClick={onRetry}>
            Try again
          </Button>
        ) : undefined
      }
    />
  )
}
