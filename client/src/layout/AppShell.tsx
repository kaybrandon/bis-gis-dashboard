import {
  BankOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  FileOutlined,
  FileTextOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuOutlined,
  MenuUnfoldOutlined,
  CloudServerOutlined,
  ApiOutlined,
  SettingOutlined,
  TeamOutlined,
  UploadOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Avatar, Button, Drawer, Dropdown, Grid, Layout, Menu } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { accountTriggerLabel } from '../accountLabel'
import { authorizedBlob } from '../api'
import { useAuth } from '../auth'
import { FloatingTimeClock } from '../components/FloatingTimeClock'
import { HeaderGreeting } from '../components/HeaderGreeting'
import { HelpMessageSheet } from '../components/HelpMessageSheet'
import { NotificationBell } from '../components/NotificationBell'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { PresencePopover, WhoIsOnlineStrip } from '../components/WhoIsOnline'
import { PresenceProvider } from '../presence'
import { usePresenceHeartbeat } from '../usePresenceHeartbeat'

const { Header, Sider, Content, Footer } = Layout

const SIDER_KEY = 'gis.siderCollapsed'

export function AppShell() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const screens = Grid.useBreakpoint()
  const [open, setOpen] = useState(false)
  const [notifyOpen, setNotifyOpen] = useState(false)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDER_KEY) === '1')
  const isMobile = !screens.lg
  const isPhone = !screens.sm
  const triggerLabel = accountTriggerLabel(user)
  const search = new URLSearchParams(location.search)
  const isPopout = search.get('popout') === '1'
  const canSeePresence = Boolean(user?.canSeePresence ?? user?.canSeeInternalNotes)

  usePresenceHeartbeat(Boolean(user) && !isPopout)

  useEffect(() => {
    if (!user?.hasAvatar) {
      setAvatarUrl(null)
      return
    }
    let revoked = false
    authorizedBlob('/api/auth/me/avatar')
      .then((blob) => {
        if (!revoked) setAvatarUrl(URL.createObjectURL(blob))
      })
      .catch(() => {
        if (!revoked) setAvatarUrl(null)
      })
    return () => {
      revoked = true
    }
  }, [user?.hasAvatar, user?.id])

  useEffect(() => () => {
    if (avatarUrl) URL.revokeObjectURL(avatarUrl)
  }, [avatarUrl])

  const items = useMemo(() => {
    const nav = [
      { key: '/', icon: <DashboardOutlined />, label: <Link to="/">Dashboard</Link> },
      { key: '/documents', icon: <FileOutlined />, label: <Link to="/documents">Manage Documents</Link> },
      ...(user?.canUpload
        ? [{ key: '/upload-documents', icon: <UploadOutlined />, label: <Link to="/upload-documents">Upload Documents</Link> }]
        : []),
      { key: '/reports', icon: <FileTextOutlined />, label: <Link to="/reports">Reports</Link> },
    ]
    if (user?.canViewTimeReport) {
      nav.push({ key: '/time-report', icon: <ClockCircleOutlined />, label: <Link to="/time-report">Time Report</Link> })
    }
    if (user?.canSeeConnections) {
      nav.push({ key: '/connections', icon: <ApiOutlined />, label: <Link to="/connections">Connections</Link> })
    }
    if (user?.canManageAssignedTechs || user?.canManageDirectory) {
      nav.push(
        { key: '/admin/organizations', icon: <BankOutlined />, label: <Link to="/admin/organizations">Organizations</Link> },
      )
    }
    if (user?.canManageDirectory) {
      nav.push(
        { key: '/admin/users', icon: <TeamOutlined />, label: <Link to="/admin/users">Users</Link> },
      )
    }
    nav.push({
      key: '/settings',
      icon: <SettingOutlined />,
      label: (
        <Link to="/settings">
          {user?.canManageGlobalDirectory ? 'Admin Settings' : 'Settings'}
        </Link>
      ),
    })
    if (user?.canManageGlobalDirectory) {
      nav.push({
        key: '/status',
        icon: <CloudServerOutlined />,
        label: <Link to="/status">Status</Link>,
      })
    }
    return nav
  }, [user])

  const selected = items
    .map((item) => item.key)
    .filter((key) => (key === '/' ? location.pathname === '/' : location.pathname.startsWith(key)))

  const menu = <Menu theme="dark" mode="inline" selectedKeys={selected} items={items} onClick={() => setOpen(false)} />

  if (isPopout) {
    return (
      <Layout className="app-shell is-popout" style={{ minHeight: '100vh' }}>
        <Content className="content-wrap is-popout">
          <Outlet />
        </Content>
      </Layout>
    )
  }

  return (
    <PresenceProvider enabled={canSeePresence} selfUserId={user?.id}>
    <Layout className="app-shell" style={{ minHeight: '100vh' }}>
      {!isMobile && (
        <Sider
          collapsible
          collapsed={collapsed}
          collapsedWidth={64}
          width={220}
          theme="dark"
          style={{ background: '#001529' }}
          trigger={null}
          onCollapse={(value) => {
            setCollapsed(value)
            localStorage.setItem(SIDER_KEY, value ? '1' : '0')
          }}
        >
          <div className="brand">
            <span className="brand-mark">GIS</span>
            {!collapsed && <span>GIS Dashboard</span>}
          </div>
          {menu}
        </Sider>
      )}
      <Layout>
        <Header className={isPhone ? 'app-header app-header-phone' : 'app-header'}>
          <div className="app-header-left">
            {isMobile ? (
              <Button type="text" aria-label="Open menu" icon={<MenuOutlined />} onClick={() => setOpen(true)} />
            ) : (
              <Button
                type="text"
                aria-label={collapsed ? 'Expand menu' : 'Collapse menu'}
                icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
                onClick={() => {
                  const next = !collapsed
                  setCollapsed(next)
                  localStorage.setItem(SIDER_KEY, next ? '1' : '0')
                }}
              />
            )}
            <HeaderGreeting user={user} />
          </div>
          <div className="app-header-right">
            {user && <PresencePopover enabled={canSeePresence} />}
            {user && <NotificationBell open={notifyOpen} onOpenChange={setNotifyOpen} />}
            <Dropdown
              trigger={['click']}
              menu={{
                items: [
                  { key: 'profile', icon: <UserOutlined />, label: 'View Profile', onClick: () => navigate('/profile') },
                  { type: 'divider' },
                  {
                    key: 'out',
                    icon: <LogoutOutlined />,
                    label: 'Sign Out',
                    onClick: () => {
                      logout()
                      navigate('/login')
                    },
                  },
                ],
              }}
            >
              <Button type="text" className="app-header-user">
                <Avatar size={24} src={avatarUrl || undefined} icon={!avatarUrl ? <UserOutlined /> : undefined} />
                {isPhone ? null : <span>{triggerLabel}</span>}
              </Button>
            </Dropdown>
          </div>
        </Header>
        {user && <WhoIsOnlineStrip enabled={canSeePresence} userId={user.id} />}
        <Content className="content-wrap">
          <Outlet />
        </Content>
        <Footer className="app-footer">
          <PoweredByFooter />
        </Footer>
        <FloatingTimeClock />
      </Layout>
      <Drawer
        open={open}
        onClose={() => setOpen(false)}
        placement="left"
        width={280}
        classNames={{ body: 'app-nav-drawer-body' }}
        styles={{ body: { padding: 0, background: '#001529' }}
      >
        <div className="brand">
          <span className="brand-mark">GIS</span>
          <span>GIS Dashboard</span>
        </div>
        {menu}
      </Drawer>
      <HelpMessageSheet />
    </Layout>
    </PresenceProvider>
  )
}
