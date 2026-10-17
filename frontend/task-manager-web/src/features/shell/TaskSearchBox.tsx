import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { ComboBox, Input, ListBox, ListBoxItem, Popover } from 'react-aria-components'
import { useNavigate } from 'react-router'
import { apiClient, call } from '../../api/client'
import { parseTaskKey, taskPath } from '../../utils/taskKey'

/** ヘッダーのタスク検索（REQ-SRC-01）。「SALES-12」と入力して Enter で直接移動できる */
export function TaskSearchBox() {
  const navigate = useNavigate()
  const [input, setInput] = useState('')
  const [term, setTerm] = useState('')
  useEffect(() => {
    const timer = setTimeout(() => setTerm(input.trim()), 250)
    return () => clearTimeout(timer)
  }, [input])
  const results = useQuery({
    queryKey: ['search', term],
    queryFn: () => call(() => apiClient.GET('/api/v1/search/tasks', { params: { query: { q: term } } })),
    enabled: term.length > 0,
    staleTime: 10_000,
  })
  const items = term.length > 0 ? (results.data ?? []) : []
  const go = (key: string) => {
    const parsed = parseTaskKey(key)
    if (parsed !== null) {
      setInput('')
      void navigate(taskPath(parsed.projectKey, parsed.number))
    }
  }
  return (
    <ComboBox
      aria-label="タスクを検索"
      inputValue={input}
      onInputChange={setInput}
      items={items}
      menuTrigger="input"
      allowsEmptyCollection
      onSelectionChange={(key) => {
        if (typeof key === 'string') go(key)
      }}
    >
      <Input
        id="tm-task-search"
        className="tm-input"
        placeholder="タスクを検索（SALES-12 で直接移動）"
        onKeyDown={(e) => {
          if (e.key === 'Enter' && parseTaskKey(input) !== null && items.length === 0) go(input)
        }}
      />
      <Popover className="tm-popover" placement="bottom start" style={{ width: 'var(--trigger-width)' }}>
        <ListBox className="tm-menu" renderEmptyState={() => (
          <div className="tm-small tm-muted" style={{ padding: 'var(--space-2) var(--space-3)' }}>
            {term.length === 0 ? '検索する言葉を入力してください' : results.isFetching ? '検索しています…' : '見つかりませんでした'}
          </div>
        )}>
          {(item: (typeof items)[number]) => (
            <ListBoxItem id={item.key} textValue={`${item.key} ${item.title}`} className="tm-menu-item">
              <span className="tm-key">{item.key}</span> {item.title}
              <div className="tm-small tm-muted">{item.project.name}・{item.status.name}</div>
            </ListBoxItem>
          )}
        </ListBox>
      </Popover>
    </ComboBox>
  )
}
