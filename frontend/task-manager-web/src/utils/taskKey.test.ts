import { describe, expect, it } from 'vitest'
import { safeNextPath } from '../features/auth/RouteGuards'
import { formatBytes, parseTaskKey, taskPath } from './taskKey'

describe('parseTaskKey（タスクキーの解析）', () => {
  it.each([
    ['SALES-12', { projectKey: 'SALES', number: 12 }],
    [' sales-1 ', { projectKey: 'SALES', number: 1 }],
    ['A1-999999999', { projectKey: 'A1', number: 999999999 }],
  ])('%s', (input, expected) => {
    expect(parseTaskKey(input)).toEqual(expected)
  })

  it.each(['SALES-0', 'SALES-01', 'S-1', 'SALES12', '1A-2', 'SALES-1234567890', 'SALES-1/../x', ''])('不正な形式 %s は null', (input) => {
    expect(parseTaskKey(input)).toBeNull()
  })

  it('画面のパス', () => {
    expect(taskPath('SALES', 12)).toBe('/projects/SALES/tasks/12')
  })
})

describe('formatBytes', () => {
  it.each([
    [512, '512 B'],
    [2048, '2.0 KB'],
    [10 * 1024 * 1024, '10.0 MB'],
  ])('%d → %s', (bytes, expected) => {
    expect(formatBytes(bytes)).toBe(expected)
  })
})

describe('safeNextPath（ログイン後の移動先。オープンリダイレクト対策）', () => {
  it.each([
    [null, '/'],
    ['/projects/SALES/tasks', '/projects/SALES/tasks'],
    ['/projects?view=board', '/projects?view=board'],
    ['//evil.example.test/', '/'],
    ['/\\evil.example.test/', '/'],
    ['/\t/evil.example.test/', '/'],
    ['https://evil.example.test/', '/'],
    ['javascript:alert(1)', '/'],
    ['/login?next=/x', '/'],
  ])('%s → %s', (next, expected) => {
    expect(safeNextPath(next)).toBe(expected)
  })
})
