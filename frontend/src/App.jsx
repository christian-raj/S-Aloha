import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { getToken } from './api'
import Layout from './components/Layout'
import Login from './pages/Login'
import Console from './pages/Console'
import Dashboard from './pages/Dashboard'
import Problems from './pages/Problems'
import ProblemDetail from './pages/ProblemDetail'
import Actions from './pages/Actions'

function Private({ children }) {
  return getToken() ? children : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<Private><Layout /></Private>}>
        <Route index element={<Console />} />
        <Route path="reports" element={<Dashboard />} />
        <Route path="problems" element={<Problems />} />
        <Route path="problems/:id" element={<ProblemDetail />} />
        <Route path="actions" element={<Actions />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
