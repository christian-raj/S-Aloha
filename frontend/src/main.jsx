import React from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import ErrorBoundary from './core/components/ErrorBoundary'
import './styles.css'

createRoot(document.getElementById('root')).render(
  <ErrorBoundary level="app"><BrowserRouter><App /></BrowserRouter></ErrorBoundary>
)
