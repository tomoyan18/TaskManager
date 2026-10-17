import type { Schemas } from '../api/client'

// 列挙値の日本語表示（docs/design/domain-model.md §3）
export const PRIORITY_LABELS: Record<Schemas['TaskPriority'], string> = {
  low: '低',
  normal: '中',
  high: '高',
  urgent: '至急',
}
export const PRIORITY_ORDER: Schemas['TaskPriority'][] = ['urgent', 'high', 'normal', 'low']

export const CATEGORY_LABELS: Record<Schemas['StatusCategory'], string> = {
  notStarted: '未着手',
  inProgress: '進行中',
  done: '完了',
  canceled: '中止',
}
export const CATEGORY_ORDER: Schemas['StatusCategory'][] = ['notStarted', 'inProgress', 'done', 'canceled']

export const COLOR_LABELS: Record<Schemas['StatusColor'], string> = {
  gray: 'グレー',
  blue: '青',
  teal: '青緑',
  green: '緑',
  yellow: '黄',
  orange: 'オレンジ',
  red: '赤',
  purple: '紫',
  pink: 'ピンク',
}
export const COLOR_ORDER = Object.keys(COLOR_LABELS) as Schemas['StatusColor'][]

export const PROJECT_ROLE_LABELS: Record<Schemas['ProjectRole'], string> = {
  viewer: '閲覧者',
  editor: '編集者',
  owner: '管理者',
}

export const SYSTEM_ROLE_LABELS: Record<Schemas['SystemRole'], string> = {
  member: '一般利用者',
  administrator: 'システム管理者',
}

export const MILESTONE_STATE_LABELS: Record<Schemas['MilestoneState'], string> = {
  open: '進行中',
  closed: '終了',
}

export const TIME_ZONES: { id: string; label: string }[] = [
  { id: 'Asia/Tokyo', label: '日本（東京）' },
  { id: 'UTC', label: '協定世界時（UTC）' },
  { id: 'Asia/Seoul', label: '韓国（ソウル）' },
  { id: 'Asia/Shanghai', label: '中国（上海）' },
  { id: 'Asia/Singapore', label: 'シンガポール' },
  { id: 'Europe/London', label: 'イギリス（ロンドン）' },
  { id: 'America/New_York', label: 'アメリカ東部（ニューヨーク）' },
  { id: 'America/Los_Angeles', label: 'アメリカ西部（ロサンゼルス）' },
]
