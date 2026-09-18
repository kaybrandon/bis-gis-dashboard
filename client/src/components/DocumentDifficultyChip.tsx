import { Tag, Tooltip } from 'antd'
import type { DocumentDifficulty } from '../api'
import { difficultyReasons, difficultyTagColor } from '../documentDifficulty'

type Props = {
  difficulty?: DocumentDifficulty | null
  showWhy?: boolean
}

export function DocumentDifficultyChip({ difficulty, showWhy }: Props) {
  if (!difficulty?.band) return null
  const reasons = difficultyReasons(difficulty)
  const why = reasons.join(' ')
  const label = difficulty.overridden ? `${difficulty.band} · override` : difficulty.band
  const tag = (
    <Tag color={difficultyTagColor(difficulty.band)} style={{ marginInlineEnd: 0 }}>
      {label}
    </Tag>
  )
  if (!why) return tag
  if (showWhy) {
    return (
      <span className="document-difficulty">
        {tag}
        <span className="document-difficulty-why">{why}</span>
      </span>
    )
  }
  return <Tooltip title={why}>{tag}</Tooltip>
}
