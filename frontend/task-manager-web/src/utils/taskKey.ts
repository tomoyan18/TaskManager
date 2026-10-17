/** 「SALES-12」形式のタスクキーを解析する（大文字小文字は問わない） */
export function parseTaskKey(input: string): { projectKey: string; number: number } | null {
  const match = /^([A-Za-z][A-Za-z0-9]{1,9})-([1-9][0-9]{0,8})$/.exec(input.trim())
  if (match === null) return null
  return { projectKey: (match[1] ?? '').toUpperCase(), number: Number(match[2]) }
}

export function taskPath(projectKey: string, number: number): string {
  return `/projects/${projectKey}/tasks/${number}`
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}
