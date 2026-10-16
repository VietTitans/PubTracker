import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource-variable/bricolage-grotesque'
import '@fontsource-variable/fraunces'
import '@fontsource-variable/jetbrains-mono'
import { MotionConfig } from 'motion/react'
import './index.css'
import App from './App.tsx'
import AuthCallback from './auth/AuthCallback.tsx'

const root = createRoot(document.getElementById('root')!)

if (window.location.pathname === '/callback') {
  root.render(<AuthCallback />)
} else {
  root.render(
    <StrictMode>
      <MotionConfig reducedMotion="user">
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </MotionConfig>
    </StrictMode>,
  )
}
