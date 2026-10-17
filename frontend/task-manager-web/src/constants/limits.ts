// 入力の上限（docs/design/api-design.md と同じ値。OpenAPI の制約とそろえる）
export const LIMITS = {
  title: 200,
  description: 20000,
  comment: 10000,
  projectName: 80,
  projectDescription: 2000,
  projectKeyMin: 2,
  projectKeyMax: 10,
  statusName: 30,
  tagName: 30,
  milestoneName: 80,
  milestoneDescription: 2000,
  displayName: 50,
  passwordMin: 12,
  passwordMax: 128,
  tagsPerTask: 10,
  attachmentBytes: 10 * 1024 * 1024,
  attachmentsPerTask: 20,
} as const

export const PROJECT_KEY_PATTERN = /^[A-Za-z][A-Za-z0-9]{1,9}$/

export const ATTACHMENT_ACCEPT = '.pdf,.png,.jpg,.jpeg,.gif,.webp,.txt,.csv,.md,.docx,.xlsx,.pptx,.zip'
