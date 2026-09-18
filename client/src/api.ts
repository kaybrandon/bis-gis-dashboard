export type OrgOption = { id: string; name: string; code: string }
export type AuthUser = {
  id: string
  email: string
  displayName: string
  userName?: string
  fullName?: string | null
  workPhone?: string | null
  hasAvatar?: boolean
  role: 'GlobalAdministrator' | 'Administrator' | 'Editor' | 'Uploader' | 'Viewer'
  roleDisplayName: string
  organizations: OrgOption[]
  canSeeInternalNotes: boolean
  canEditInternalNotes: boolean
  canSeeTimeLogs: boolean
  canLogTime: boolean
  canUpload: boolean
  canMutateWorkItems: boolean
  canManageDirectory: boolean
  canManageGlobalDirectory: boolean
  canManageAssignedTechs?: boolean
  canViewTimeReport: boolean
  canViewTeamTimeReport: boolean
  canSeeAllOrganizations: boolean
  canSeePresence?: boolean
  canSeeConnections?: boolean
  canManageConnections?: boolean
  canPostComments?: boolean
  canSeeDashboardAssignee?: boolean
}

export type NamedCount = { id: string; name: string; color?: string | null; count: number }
export type WorkItemListItem = {
  id: string
  fileName: string
  title: string
  organizationId: string
  organizationName: string
  documentTypeId: string
  documentTypeName: string
  statusId: string
  statusName: string
  statusColor: string
  assignedToName?: string | null
  assignedToUserId?: string | null
  priorityNeededBy?: string | null
  uploadedAt: string
  uploadedByName: string
  updatedAt: string
  workedOn?: string | null
  hours: number
  hoursLabel: string
  firstDeadline?: string | null
  finalDeadline?: string | null
  contentType?: string | null
  fileSizeBytes: number
  isPriority?: boolean
  priorityNote?: string | null
  isReviewed?: boolean
}

export type BucketCounts = {
  pending: number
  mine: number
  onHold: number
  completed: number
  firstDeadline: number
  finalDeadline: number
  priority: number
  dueThisWeek: number
  unassigned: number
}

export type WorkItemListResponse = {
  items: WorkItemListItem[]
  total: number
  page: number
  pageSize: number
  statusCounts: NamedCount[]
  typeCounts: NamedCount[]
  buckets: BucketCounts
}

export type DashboardKpi = { key: string; label: string; count: number; color?: string | null }
export type DashboardQuery = Pick<WorkItemQuery, 'organizationId' | 'statusId' | 'assignedToUserId' | 'unassignedOnly'> & {
  from?: string
  to?: string
}

export type DashboardResponse = {
  kpis: DashboardKpi[]
  statusCounts: NamedCount[]
  assigneeCounts: NamedCount[]
  organizationCounts: NamedCount[]
  recentCompleted: WorkItemListItem[]
  volumeOverTime: Array<{ date: string; uploaded: number; completed: number }>
  hoursByAssignee: Array<{ id: string; name: string; hours: number }>
  hoursByClient: Array<{ id: string; name: string; hours: number }>
  from: string
  to: string
  rangeLabel: string
}

export type DashboardRecipient = { id: string; displayName: string; email: string }
export type DashboardEmailResult = { delivered: boolean; mode: string; recipients: string; note?: string | null }

export type UploadLink = {
  organizationId: string
  organizationName: string
  token: string
  path: string
  createdAt?: string | null
}

export type AssignedTechnician = { name: string; isPrimary?: boolean }

export type PublicUploadInfo = {
  organizationName: string
  documentTypes: LookupItem[]
  maxFileBytes: number
  maxFileMegabytes: number
  concurrency: number
  assignedTechnicians?: AssignedTechnician[]
  acceptedExtensions?: string[]
  supportedTypesLabel?: string
  accept?: string
}

export type PublicUploadResult = {
  id: string
  fileName: string
  organizationName: string
  statusName: string
}

export type EnvironmentCheckStatus = 'ok' | 'degraded' | 'down' | 'not_configured'
export type EnvironmentCheck = {
  key: string
  name: string
  status: EnvironmentCheckStatus | string
  mode: 'live' | 'configured' | string
  detail: string
  error?: string | null
}
export type EnvironmentStatus = {
  overall: EnvironmentCheckStatus | string
  checkedAt: string
  note: string
  checks: EnvironmentCheck[]
}

export type EmailSettings = {
  status: 'configured' | 'not_configured' | string
  configured: boolean
  enabled: boolean
  host?: string | null
  port: number
  useSsl: boolean
  from?: string | null
  fromName?: string | null
  user?: string | null
  passwordConfigured: boolean
  passwordSource: 'none' | 'secure-store' | 'app-settings' | 'key-vault' | string
  replyTo?: string | null
  updatedAt?: string | null
  note: string
}

export type SaveEmailSettings = {
  host?: string | null
  port: number
  useSsl: boolean
  from?: string | null
  fromName?: string | null
  user?: string | null
  password?: string | null
  replyTo?: string | null
}

export type EmailTestResult = { delivered: boolean; mode: string; recipients: string; note: string }

export type CompanyContact = {
  name: string
  phone?: string | null
  email?: string | null
  address?: string | null
  website?: string | null
  updatedAt?: string | null
}

export type StatusActions = {
  pendingId: string
  activeId: string
  onHoldId: string
  completeId: string
  cancelledId: string
  needsReviewId: string
  completedIds: string[]
}

export type Comment = {
  id: string
  workItemId: string
  body: string
  authorUserId: string
  authorName: string
  createdAt: string
}

export type NoteRevision = {
  id: string
  body: string
  editedAt: string
  editedByName: string
}

export type WorkItemDetail = WorkItemListItem & {
  assignedToUserId?: string | null
  internalNotes?: string | null
  canSeeInternalNotes: boolean
  canEditInternalNotes: boolean
  internalNotesUpdatedAt?: string | null
  internalNotesUpdatedByName?: string | null
  internalNotesHistory: NoteRevision[]
  canSeeTimeLogs: boolean
  canLogTime: boolean
  canMutate: boolean
  canPostComments: boolean
  isSplit: boolean
  isSketch: boolean
  annexationCount: number
  correctionCount: number
  deedCount: number
  platCount: number
  propertyIds?: string | null
  isPriority?: boolean
  priorityNote?: string | null
  priorityRequestedAt?: string | null
  priorityRequestedByName?: string | null
  canSetPriority?: boolean
  priorityNeededBy?: string | null
}

export type TimeEntry = {
  id: string
  workItemId: string
  minutes: number
  hours: number
  durationLabel: string
  workedOn: string
  note?: string | null
  loggedByUserId: string
  loggedByName: string
  createdAt: string
  updatedAt: string
  canEdit: boolean
}

export type TimeEntryList = {
  items: TimeEntry[]
  totalMinutes: number
  totalLabel: string
}

export type UpsertTimeEntry = {
  hours?: number
  minutes?: number
  decimalHours?: number
  workedOn?: string
  note?: string
}

export type AiFillStringField = { present: boolean; value?: string | null; confidence: number }
export type AiFillIntField = { present: boolean; value?: number | null; confidence: number }
export type AiFillDateField = { present: boolean; value?: string | null; confidence: number }
export type AiFillTypeField = {
  present: boolean
  documentTypeId?: string | null
  documentTypeName?: string | null
  confidence: number
}
export type AiFillResponse = {
  overallConfidence: number
  deployment: string
  warning?: string | null
  fields: {
    title: AiFillStringField
    type: AiFillTypeField
    propertyIds: AiFillStringField
    annexationCount: AiFillIntField
    correctionCount: AiFillIntField
    deedCount: AiFillIntField
    platCount: AiFillIntField
    workedOn: AiFillDateField
  }
}

export type LookupItem = { id: string; name: string; color?: string | null; sortOrder: number }
export type AssignableUser = { id: string; displayName: string; email: string }

export type ReportCadence = 'Monthly' | 'Annual'

export type ReportListItem = {
  id: string
  organizationId: string
  organizationName: string
  cadence: ReportCadence | string
  year: number
  month: number
  version: number
  monthLabel: string
  generatedAt: string
  generatedByName: string
  lastEmailedAt?: string | null
  lastEmailedTo?: string | null
  emailCount: number
  emailed: boolean
}

export type ReportNamedCount = { name: string; count: number; color?: string | null }

export type ReportContact = {
  phone: string
  email: string
  department: string
  company: string
  website: string
}

export type ReportParcelStatus = {
  available: boolean
  note: string
  totalRealAccounts?: number | null
  parcelsWithOwnership?: number | null
  missingRealAccounts?: number | null
  percentComplete?: number | null
}

export type ReportCompletedItem = {
  fileName: string
  uploadedAt: string
  workedOn?: string | null
  annexations: number
  corrections: number
  plats: number
  deeds: number
  sketch: boolean
  propertyIds: string
}

export type ReportSnapshot = {
  title: string
  cadence: ReportCadence | string
  includeParcelStatus: boolean
  organizationName: string
  monthName: string
  monthLabel: string
  periodPhrase: string
  intro: string
  closing: string
  contact: ReportContact
  parcelStatus: ReportParcelStatus
  completed: number
  maintenanceByType: ReportNamedCount[]
  completedItems: ReportCompletedItem[]
  uploaded: number
  pending: number
  active: number
  onHold: number
  hours: number
  hoursLabel: string
}

export type ReportEmailLog = {
  id: string
  sentAt: string
  recipients: string
  mode: string
  delivered: boolean
  error?: string | null
}

export type ReportDetail = ReportListItem & {
  canEmail: boolean
  snapshot: ReportSnapshot
  emails: ReportEmailLog[]
}

export type ReportRecipient = { id: string; displayName: string; email: string }

export type EmailReportResult = {
  delivered: boolean
  mode: string
  recipients: string
  note?: string | null
}

export type TimeReportBucket = 'day' | 'week' | 'month'

export type TimeReportQuery = {
  from?: string
  to?: string
  organizationId?: string
  userId?: string
  bucket?: TimeReportBucket
}

export type TimeReportNamedTotal = {
  id: string
  name: string
  minutes: number
  hours: number
  hoursLabel: string
  entryCount: number
}

export type TimeReportPeriodTotal = {
  key: string
  label: string
  minutes: number
  hours: number
  hoursLabel: string
  entryCount: number
}

export type TimeReportWorkItemTotal = {
  id: string
  fileName: string
  organizationName: string
  minutes: number
  hours: number
  hoursLabel: string
  entryCount: number
}

export type TimeReportLine = {
  workedOn: string
  loggedByName: string
  organizationName: string
  fileName: string
  workItemId: string
  minutes: number
  hours: number
  hoursLabel: string
  note?: string | null
}

export type TimeReportPersonOption = { id: string; name: string }

export type TimeReportResponse = {
  from: string
  to: string
  bucket: TimeReportBucket | string
  canViewTeam: boolean
  totalMinutes: number
  totalHours: number
  totalLabel: string
  peopleCount: number
  clientCount: number
  workItemCount: number
  people: TimeReportPersonOption[]
  byPerson: TimeReportNamedTotal[]
  byClient: TimeReportNamedTotal[]
  byPeriod: TimeReportPeriodTotal[]
  byWorkItem: TimeReportWorkItemTotal[]
  entries: TimeReportLine[]
}

export type WorkItemQuery = {
  search?: string
  organizationId?: string
  documentTypeId?: string
  statusId?: string
  assignedToUserId?: string
  unassignedOnly?: boolean
  uploadedFrom?: string
  uploadedTo?: string
  workedFrom?: string
  workedTo?: string
  bucket?: string
  groupBy?: string
  sortBy?: string
  sortDir?: string
  page?: number
  pageSize?: number
}

const TOKEN_KEY = 'gis.token'

export function getToken() {
  const local = localStorage.getItem(TOKEN_KEY)
  if (local) return local
  const session = sessionStorage.getItem(TOKEN_KEY)
  if (session) {
    localStorage.setItem(TOKEN_KEY, session)
    sessionStorage.removeItem(TOKEN_KEY)
    return session
  }
  return null
}

export function setToken(token: string | null) {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token)
    sessionStorage.removeItem(TOKEN_KEY)
  } else {
    localStorage.removeItem(TOKEN_KEY)
    sessionStorage.removeItem(TOKEN_KEY)
  }
}

export function queryString(query: Record<string, string | number | boolean | undefined | null>) {
  const params = new URLSearchParams()
  const entries = { ...query }
  if (entries.assignedToUserId === 'unassigned') {
    delete entries.assignedToUserId
    entries.unassignedOnly = true
  }
  Object.entries(entries).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      params.set(key, String(value))
    }
  })
  return params.toString()
}

function friendlyHttpMessage(status: number, bodyMessage?: string) {
  if (bodyMessage?.trim() && (status === 502 || status === 503 || status === 504)) {
    return bodyMessage.trim()
  }
  if (status === 502 || status === 503 || status === 504) {
    return 'GIS Dashboard is temporarily unavailable. Wait a moment and try again.'
  }
  if (status === 429) {
    return 'Too many requests. Wait a moment and try again.'
  }
  if (bodyMessage?.trim()) return bodyMessage
  if (status === 404) return 'That was not found.'
  if (status === 403) return 'You do not have access to that.'
  return `Something went wrong (${status}). Try again.`
}

async function readErrorMessage(response: Response) {
  try {
    const body = await response.json()
    return friendlyHttpMessage(response.status, typeof body?.message === 'string' ? body.message : undefined)
  } catch {
    return friendlyHttpMessage(response.status)
  }
}

function networkError(): Error {
  return new Error('Could not reach GIS Dashboard. Check your connection and try again.')
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (!headers.has('Content-Type') && init?.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(path, { ...init, headers })
  } catch {
    throw networkError()
  }
  if (response.status === 401) {
    setToken(null)
    const publicAuth = path.startsWith('/api/auth/login')
      || path.startsWith('/api/auth/forgot-password')
      || path.startsWith('/api/auth/reset-password')
      || path.startsWith('/api/auth/password-reset')
    if (!publicAuth) {
      window.location.assign('/login')
    }
  }

  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  login: (email: string, password: string) =>
    request<{ token: string; expiresAt: string; user: AuthUser }>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  passwordResetStatus: () => request<{ available: boolean; message: string }>('/api/auth/password-reset'),
  forgotPassword: (emailOrUsername: string) =>
    request<{ message: string }>('/api/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify({ emailOrUsername }),
    }),
  resetPassword: (token: string, newPassword: string, confirmPassword: string) =>
    request<{ message: string }>('/api/auth/reset-password', {
      method: 'POST',
      body: JSON.stringify({ token, newPassword, confirmPassword }),
    }),
  me: () => request<AuthUser>('/api/auth/me'),
  updateProfile: (body: {
    userName: string
    fullName?: string | null
    email: string
    workPhone?: string | null
    currentPassword?: string
    newPassword?: string
    confirmPassword?: string
  }) => request<AuthUser>('/api/auth/me', { method: 'PUT', body: JSON.stringify(body) }),
  uploadAvatar: (form: FormData) => request<AuthUser>('/api/auth/me/avatar', { method: 'POST', body: form }),
  dashboard: (query: DashboardQuery) =>
    request<DashboardResponse>(`/api/dashboard?${queryString(query)}`),
  dashboardPdf: async (query: DashboardQuery) => {
    const blob = await authorizedBlob(`/api/dashboard/pdf?${queryString(query)}`)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'gis-dashboard.pdf'
    link.click()
    URL.revokeObjectURL(url)
  },
  dashboardRecipients: (organizationId?: string) =>
    request<DashboardRecipient[]>(`/api/dashboard/recipients${organizationId ? `?organizationId=${organizationId}` : ''}`),
  emailDashboard: (query: DashboardQuery, body: { userIds: string[]; extraEmails: string[] }) =>
    request<DashboardEmailResult>(`/api/dashboard/email?${queryString(query)}`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  workItems: (query: WorkItemQuery) =>
    request<WorkItemListResponse>(`/api/work-items?${queryString(query)}`),
  exportWorkItems: async (query: WorkItemQuery) => {
    const token = getToken()
    const response = await fetch(`/api/work-items/export?${queryString(query)}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    })
    if (!response.ok) throw new Error('Export failed.')
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'gis-work-items.csv'
    link.click()
    URL.revokeObjectURL(url)
  },
  workItem: (id: string) => request<WorkItemDetail>(`/api/work-items/${id}`),
  aiFillFromPdf: (id: string) =>
    request<AiFillResponse>(`/api/work-items/${id}/ai-fill`, { method: 'POST' }),
  neighbors: (id: string, query: WorkItemQuery) =>
    request<{ previousId?: string | null; nextId?: string | null }>(
      `/api/work-items/${id}/neighbors?${queryString(query)}`,
    ),
  updateWorkItem: (id: string, body: Record<string, unknown>) =>
    request<WorkItemDetail>(`/api/work-items/${id}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    }),
  upload: (form: FormData) =>
    request<WorkItemDetail>('/api/work-items', { method: 'POST', body: form }),
  fileUrl: (id: string) => `/api/work-items/${id}/file`,
  documentTypes: () => request<LookupItem[]>('/api/lookups/document-types'),
  statuses: () => request<LookupItem[]>('/api/lookups/statuses'),
  organizations: () => request<OrgOption[]>('/api/lookups/organizations'),
  assignableUsers: (organizationId: string) =>
    request<AssignableUser[]>(`/api/lookups/assignable-users?organizationId=${organizationId}`),
  assignedTechnicians: (organizationId: string) =>
    request<AssignedTechnician[]>(`/api/lookups/assigned-technicians?organizationId=${organizationId}`),
  assignees: () => request<AssignableUser[]>('/api/lookups/assignees'),
  statusActions: () => request<StatusActions>('/api/lookups/status-actions'),
  comments: (workItemId: string) => request<Comment[]>(`/api/work-items/${workItemId}/comments`),
  createComment: (workItemId: string, body: string) =>
    request<Comment>(`/api/work-items/${workItemId}/comments`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    }),
  timeEntries: (workItemId: string) =>
    request<TimeEntryList>(`/api/work-items/${workItemId}/time-entries`),
  createTimeEntry: (workItemId: string, body: UpsertTimeEntry) =>
    request<TimeEntry>(`/api/work-items/${workItemId}/time-entries`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  updateTimeEntry: (workItemId: string, entryId: string, body: UpsertTimeEntry) =>
    request<TimeEntry>(`/api/work-items/${workItemId}/time-entries/${entryId}`, {
      method: 'PATCH',
      body: JSON.stringify(body),
    }),
  deleteTimeEntry: (workItemId: string, entryId: string) =>
    request<void>(`/api/work-items/${workItemId}/time-entries/${entryId}`, { method: 'DELETE' }),
  settings: () => request<Record<string, unknown>>('/api/settings'),
  environmentStatus: () => request<EnvironmentStatus>('/api/admin/status'),
  emailSettings: () => request<EmailSettings>('/api/settings/email'),
  saveEmailSettings: (body: SaveEmailSettings) =>
    request<EmailSettings>('/api/settings/email', { method: 'PUT', body: JSON.stringify(body) }),
  sendTestEmail: (to: string) =>
    request<EmailTestResult>('/api/settings/email/test', { method: 'POST', body: JSON.stringify({ to }) }),
  companyContact: () => request<CompanyContact>('/api/settings/company'),
  saveCompanyContact: (body: { phone?: string | null; email?: string | null; address?: string | null; website?: string | null }) =>
    request<CompanyContact>('/api/settings/company', { method: 'PUT', body: JSON.stringify(body) }),
  adminUsers: (includeArchived = false) =>
    request<Array<Record<string, unknown>>>(`/api/admin/users${includeArchived ? '?includeArchived=true' : ''}`),
  adminOrgs: () => request<Array<Record<string, unknown>>>('/api/admin/organizations'),
  createOrg: (name: string, code: string) =>
    request('/api/admin/organizations', {
      method: 'POST',
      body: JSON.stringify({ name, code }),
    }),
  updateOrg: (id: string, body: {
    name: string
    code?: string
    isActive?: boolean
    parcelTotalRealAccounts?: number | null
    parcelWithOwnership?: number | null
    timeReportCardsVisible?: boolean
    assignedTechIds?: string[]
    primaryAssignedTechId?: string | null
  }) =>
    request(`/api/admin/organizations/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  orgUploadLink: (id: string) => request<UploadLink>(`/api/admin/organizations/${id}/upload-link`),
  regenerateOrgUploadLink: (id: string) =>
    request<UploadLink>(`/api/admin/organizations/${id}/upload-link/regenerate`, { method: 'POST' }),
  createUser: (body: Record<string, unknown>) =>
    request('/api/admin/users', { method: 'POST', body: JSON.stringify(body) }),
  updateUser: (id: string, body: Record<string, unknown>) =>
    request(`/api/admin/users/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  archiveUser: (id: string) =>
    request(`/api/admin/users/${id}/archive`, { method: 'POST' }),
  restoreUser: (id: string) =>
    request(`/api/admin/users/${id}/restore`, { method: 'POST' }),
  updateUserOrgs: (id: string, organizationIds: string[]) =>
    request(`/api/admin/users/${id}/organizations`, {
      method: 'PUT',
      body: JSON.stringify({ organizationIds }),
    }),
  reports: (organizationId?: string) =>
    request<ReportListItem[]>(`/api/reports${organizationId ? `?organizationId=${organizationId}` : ''}`),
  report: (id: string) => request<ReportDetail>(`/api/reports/${id}`),
  generateReport: (organizationId: string, cadence: ReportCadence, year: number, month?: number) =>
    request<ReportDetail>('/api/reports/generate', {
      method: 'POST',
      body: JSON.stringify({ organizationId, cadence, year, month }),
    }),
  reportRecipients: (organizationId: string) =>
    request<ReportRecipient[]>(`/api/reports/recipients?organizationId=${organizationId}`),
  emailReport: (id: string, body: { userIds: string[]; extraEmails: string[] }) =>
    request<EmailReportResult>(`/api/reports/${id}/email`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
  downloadReportPdf: (id: string) => downloadAuthorized(`/api/reports/${id}/pdf`),
  downloadReportCsv: (id: string) => downloadAuthorized(`/api/reports/${id}/csv`),
  timeReport: (query: TimeReportQuery) =>
    request<TimeReportResponse>(`/api/time-report?${queryString(query)}`),
  exportTimeReport: (query: TimeReportQuery) =>
    downloadAuthorized(`/api/time-report/export?${queryString(query)}`),
  notifications: (unreadOnly = false) =>
    request<NotificationList>(`/api/notifications${unreadOnly ? '?unreadOnly=true' : ''}`),
  readNotification: (id: string) =>
    request<void>(`/api/notifications/${id}/read`, { method: 'POST' }),
  readAllNotifications: () =>
    request<void>('/api/notifications/read-all', { method: 'POST' }),
  heartbeat: (body: PresenceHeartbeat) =>
    request<void>('/api/presence', { method: 'POST', body: JSON.stringify(body) }),
  setNeedsHelp: (needsHelp: boolean) =>
    request<{ needsHelp: boolean }>('/api/presence/need-help', {
      method: 'POST',
      body: JSON.stringify({ needsHelp }),
    }),
  helpInbox: () => request<HelpInbox>('/api/help-messages/inbox'),
  helpThread: (withUserId: string) =>
    request<HelpThread>(`/api/help-messages?withUserId=${encodeURIComponent(withUserId)}`),
  sendHelpMessage: (body: { toUserId: string; chip?: string | null; body?: string | null }) =>
    request<HelpMessage>('/api/help-messages', { method: 'POST', body: JSON.stringify(body) }),
  connections: () => request<FileConnection[]>('/api/connections'),
  createConnection: (body: Record<string, unknown>) =>
    request<FileConnection>('/api/connections', { method: 'POST', body: JSON.stringify(body) }),
  updateConnection: (id: string, body: Record<string, unknown>) =>
    request<FileConnection>(`/api/connections/${id}`, { method: 'PATCH', body: JSON.stringify(body) }),
  runConnectionNow: (id: string) =>
    request<FileConnection>(`/api/connections/${id}/run-now`, { method: 'POST' }),
  checkFolders: (body: { sourcePath: string; remoteFolder: string; fileServerId?: string }) =>
    request<CheckFoldersResponse>('/api/connections/check-folders', { method: 'POST', body: JSON.stringify(body) }),
  fileServers: () => request<FileServer[]>('/api/file-servers'),
  fileKinds: () => request<{ kinds: FileKind[] }>('/api/settings/file-kinds'),
  ftpSettings: () => request<{ nightlyHourUtc: number }>('/api/ftp/settings'),
  lanConnections: () => request<LanConnection[]>('/api/lan-connections'),
  createLanConnection: (body: Record<string, unknown>) =>
    request<LanConnection>('/api/lan-connections', { method: 'POST', body: JSON.stringify(body) }),
  updateLanConnection: (id: string, body: Record<string, unknown>) =>
    request<LanConnection>(`/api/lan-connections/${id}`, { method: 'PATCH', body: JSON.stringify(body) }),
  runLanNow: (id: string) => request<LanConnection>(`/api/lan-connections/${id}/run-now`, { method: 'POST' }),
  retryLan: (id: string) => request<LanConnection>(`/api/lan-connections/${id}/retry`, { method: 'POST' }),
  rotateLanToken: (id: string) => request<LanConnection>(`/api/lan-connections/${id}/rotate-token`, { method: 'POST' }),
  checkLanFolders: (id: string) => request<CheckFoldersResponse>(`/api/lan-connections/${id}/check-folders`, { method: 'POST' }),
  lanMap: () => request<unknown[]>('/api/lan-connections/map'),
  syncControl: () => request<SyncControl>('/api/sync-control'),
  pauseSync: () => request<SyncControl>('/api/sync-control/pause', { method: 'POST' }),
  resumeSync: () => request<SyncControl>('/api/sync-control/resume', { method: 'POST' }),
  downloadWindowsInstaller: () => downloadAuthorized('/api/lan-connections/agent/windows-installer'),
  downloadWindowsAgentZip: () => downloadAuthorized('/api/lan-connections/agent/windows'),
  presence: () => request<PresenceList>('/api/presence'),
}

export type AppNotification = {
  id: string
  kind: string
  title: string
  body: string
  organizationId: string
  workItemId?: string | null
  createdAt: string
  unread: boolean
}

export type NotificationList = {
  items: AppNotification[]
  unreadCount: number
}

export type PresenceHeartbeat = {
  route: string
  workItemId?: string | null
  clockedIn: boolean
  clockWorkItemId?: string | null
}

export type PresenceUser = {
  userId: string
  displayName: string
  hasAvatar: boolean
  presenceStatus: 'Online' | 'Away' | 'Offline' | string
  pageName: string
  workItemId?: string | null
  workItemTitle?: string | null
  clockedIn: boolean
  lastSeen: string
  needsHelp: boolean
  unreadHelpCount: number
}

export type PresenceList = {
  items: PresenceUser[]
  onlineCount: number
  needsHelpCount: number
}

export const HELP_SEND_CHIPS = [
  { chip: 'need-help', label: 'Need help?' },
  { chip: 'take-a-look', label: 'Can you take a look?' },
] as const

export const HELP_REPLY_CHIPS = [
  { chip: 'on-my-way', label: 'On my way' },
  { chip: 'ping-5', label: 'Ping me in 5' },
  { chip: 'cant-right-now', label: "Can't right now" },
] as const

export const HELP_OFFLINE_REASON = "Offline — try when they're back."

export type HelpMessage = {
  id: string
  fromUserId: string
  toUserId: string
  chip?: string | null
  chipLabel: string
  body: string
  createdAt: string
  mine: boolean
}

export type HelpThread = {
  withUserId: string
  withDisplayName: string
  presenceStatus: string
  canCompose: boolean
  composeDisabledReason?: string | null
  items: HelpMessage[]
}

export type HelpInboxThread = {
  withUserId: string
  withDisplayName: string
  presenceStatus: string
  canCompose: boolean
  composeDisabledReason?: string | null
  preview: string
  lastAt: string
  unread: boolean
  unreadCount: number
}

export type HelpInbox = {
  unreadCount: number
  items: HelpInboxThread[]
}

export type FolderCheckResult = { ok: boolean; result: 'Pass' | 'Fail' | string; message: string; kind: string }
export type CheckFoldersResponse = {
  source: FolderCheckResult
  destination: FolderCheckResult
  bothPassed: boolean
  canRunNow: boolean
}

export type FileConnection = {
  id: string
  organizationId: string
  organizationName: string
  fileServerId?: string | null
  sourceRoot?: string | null
  fileServerRoot?: string | null
  sourcePath: string
  ftpFolder?: string | null
  ftpUrl?: string | null
  ftpUserName?: string | null
  passwordConfigured: boolean
  enabled: boolean
  status?: string | null
  lastError?: string | null
  lastPublishedAt?: string | null
  lastFileCount: number
  lastZipName?: string | null
  hasPackage: boolean
  canDownload: boolean
  canManage: boolean
}

export type AgentTelemetry = {
  hostName?: string | null
  osDescription?: string | null
  osVersion?: string | null
  arch?: string | null
  runtimeVersion?: string | null
  agentVersion?: string | null
  freeDiskBytes?: number | null
  lastError?: string | null
  localIp?: string | null
  publicIp?: string | null
  windowsUserName?: string | null
}

export type LanConnection = {
  id: string
  organizationId: string
  organizationName: string
  remoteFolder: string
  bisFolder: string
  direction: string
  scheduleMinutes: number
  enrolled: boolean
  enrollToken?: string | null
  enrollTokenMasked?: string | null
  status: string
  heartbeatOk: boolean
  lastHeartbeatAt?: string | null
  heartbeatLabel?: string | null
  heartbeatFresh?: boolean
  windowsUserName?: string | null
  lastSyncAt?: string | null
  lastPullCount: number
  lastPushCount: number
  lastError?: string | null
  lastErrorCode?: string | null
  lastErrorAt?: string | null
  machineName?: string | null
  localIp?: string | null
  publicIp?: string | null
  agentVersion?: string | null
  telemetry?: AgentTelemetry | null
  restartPending?: boolean
  runNowQueued?: boolean
  canManage: boolean
}

export type FileServer = { id: string; name: string; rootPath: string; sourceRoot: string }
export type FileKind = { key: string; displayName: string; extensions: string; enabled: boolean }
export type SyncControl = { paused: boolean; message?: string | null }

async function downloadAuthorized(path: string) {
  const token = getToken()
  let response: Response
  try {
    response = await fetch(path, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    })
  } catch {
    throw networkError()
  }
  if (!response.ok) throw new Error(await readErrorMessage(response))
  const blob = await response.blob()
  const match = /filename="?([^"]+)"?/i.exec(response.headers.get('content-disposition') ?? '')
  const name = match?.[1] ?? path.split('/').pop() ?? 'download'
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = decodeURIComponent(name)
  link.click()
  URL.revokeObjectURL(url)
}

async function publicRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  if (!headers.has('Content-Type') && init?.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }
  let response: Response
  try {
    response = await fetch(path, { ...init, headers })
  } catch {
    throw networkError()
  }
  if (!response.ok) {
    throw new Error(await readErrorMessage(response))
  }
  return response.json() as Promise<T>
}

export const publicApi = {
  company: () => publicRequest<CompanyContact>('/api/public/company'),
  info: (token: string) => publicRequest<PublicUploadInfo>(`/api/public/uploads/${encodeURIComponent(token)}`),
  upload: (token: string, form: FormData) =>
    publicRequest<PublicUploadResult>(`/api/public/uploads/${encodeURIComponent(token)}`, {
      method: 'POST',
      body: form,
    }),
  received: (token: string, body: { fileCount: number; fileNames: string[] }) =>
    publicRequest<{ accepted: boolean }>(`/api/public/uploads/${encodeURIComponent(token)}/received`, {
      method: 'POST',
      body: JSON.stringify(body),
    }),
}

export async function authorizedBlob(path: string) {
  const token = getToken()
  const response = await fetch(path, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })
  if (!response.ok) throw new Error(await readErrorMessage(response))
  return response.blob()
}
