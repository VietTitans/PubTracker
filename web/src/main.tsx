import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import AuthCallback from './auth/AuthCallback.tsx'

const root = createRoot(document.getElementById('root')!)

if (window.location.pathname === '/callback') {
  root.render(<AuthCallback />)
} else {
  root.render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
}
