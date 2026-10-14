import React from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'

vi.mock('../../api', () => ({ api: {} }))
import { SlaBadge } from './config'

afterEach(cleanup)

describe('État SLA (INC-24)', () => {
  it('marque un SLA dépassé ou en risque, rien sans SLA', () => {
    const { container } = render(<><SlaBadge state="SLA dépassé" /><SlaBadge state="SLA en risque" /><SlaBadge state="Sans SLA" /></>)
    expect(screen.getByText('SLA dépassé').className).toContain('t-bad')
    expect(screen.getByText('SLA en risque').className).toContain('t-wait')
    expect(container.querySelectorAll('.badge').length).toBe(2)
  })
})
