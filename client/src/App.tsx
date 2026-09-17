import { App as AntApp, ConfigProvider, Spin, theme } from 'antd'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth'
import { AppShell } from './layout/AppShell'
import { DashboardPage } from './pages/DashboardPage'
import { ForgotPasswordPage } from './pages/ForgotPasswordPage'
import { LoginPage } from './pages/LoginPage'
import { ResetPasswordPage } from './pages/ResetPasswordPage'
import { ManageDocumentsPage } from './pages/ManageDocumentsPage'
import { OrganizationsPage } from './pages/OrganizationsPage'
import { SettingsPage } from './pages/SettingsPage'
import { StatusPage } from './pages/StatusPage'
import { UsersPage } from './pages/UsersPage'
import { PublicUploadPage } from './pages/PublicUploadPage'
import { ReportDetailPage } from './pages/ReportDetailPage'
import { ReportsPage } from './pages/ReportsPage'
import { TimeReportPage } from './pages/TimeReportPage'
import { UploadDocumentsPage } from './pages/UploadDocumentsPage'
import { ViewDocumentPage } from './pages/ViewDocumentPage'
import { ProfilePage } from './pages/ProfilePage'
import { ConnectionsPage } from './pages/ConnectionsPage'

const maskTokens = {
  algorithm: theme.compactAlgorithm,
  token: {
    colorPrimary: '#1890ff',
    colorSuccess: '#52c41a',
    colorWarning: '#faad14',
    colorError: '#f5222d',
    colorInfo: '#1890ff',
    colorBgLayout: '#f0f2f5',
    colorBgContainer: '#ffffff',
    colorText: 'rgba(0,0,0,0.85)',
    colorTextSecondary: 'rgba(0,0,0,0.45)',
    colorBorder: '#d9d9d9',
    colorBorderSecondary: '#f0f0f0',
    borderRadius: 2,
    fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
    controlHeight: 28,
    marginLG: 12,
    paddingLG: 16,
  },
  components: {
    Form: {
      itemMarginBottom: 12,
      verticalLabelPadding: '0 0 2px',
    },
    Card: {
      headerHeight: 40,
      headerHeightSM: 32,
    },
  },
}

function PrivateRoutes() {
  const { user, loading } = useAuth()
  if (loading) {
    return <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center' }}><Spin /></div>
  }
  if (!user) return <Navigate to="/login" replace />
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/documents" element={<ManageDocumentsPage />} />
        <Route path="/documents/:id" element={<ViewDocumentPage />} />
        <Route path="/upload-documents" element={user.canUpload ? <UploadDocumentsPage /> : <Navigate to="/" replace />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/reports/:id" element={<ReportDetailPage />} />
        <Route path="/time-report" element={user.canViewTimeReport ? <TimeReportPage /> : <Navigate to="/" replace />} />
        <Route path="/profile" element={<ProfilePage />} />
        <Route path="/settings" element={<SettingsPage />} />
        <Route path="/status" element={user.canManageGlobalDirectory ? <StatusPage /> : <Navigate to="/" replace />} />
        <Route path="/admin/users" element={user.canManageDirectory ? <UsersPage /> : <Navigate to="/" replace />} />
        <Route path="/admin/organizations" element={user.canManageDirectory ? <OrganizationsPage /> : <Navigate to="/" replace />} />
        <Route path="/connections" element={user.canSeeConnections ? <ConnectionsPage /> : <Navigate to="/" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <ConfigProvider theme={maskTokens}>
      <AntApp>
        <AuthProvider>
          <BrowserRouter>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/forgot-password" element={<ForgotPasswordPage />} />
              <Route path="/reset-password" element={<ResetPasswordPage />} />
              <Route path="/upload/:token" element={<PublicUploadPage />} />
              <Route path="/*" element={<PrivateRoutes />} />
            </Routes>
          </BrowserRouter>
        </AuthProvider>
      </AntApp>
    </ConfigProvider>
  )
}
