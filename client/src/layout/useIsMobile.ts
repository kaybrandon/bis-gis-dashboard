import { Grid } from 'antd'

/** Phone-width layout (~390px). Ant Design `sm` starts at 576px. */
export function useIsMobile() {
  const screens = Grid.useBreakpoint()
  return !screens.sm
}
