#!/usr/bin/env node

/**
 * Copia o build da landing page para o PocketBase.
 *
 * Origem: apps/landing/publish/wwwroot  (saída de `dotnet publish`)
 * Destino: pocketbase/pb_public
 */

import { copyFileSync, mkdirSync, existsSync, readdirSync, statSync, rmSync } from 'fs'
import { join } from 'path'
import { fileURLToPath } from 'url'
import { dirname } from 'path'

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const rootDir = join(__dirname, '..')

const sourceDir = join(rootDir, 'apps', 'landing', 'publish', 'wwwroot')
const targetDir = join(rootDir, 'pocketbase', 'pb_public')

function copyRecursive(src, dest) {
  if (!existsSync(src)) {
    console.error(`❌ Diretório source não encontrado: ${src}`)
    console.log('💡 Execute primeiro: npm run build:landing')
    process.exit(1)
  }

  const stats = statSync(src)

  if (stats.isDirectory()) {
    if (!existsSync(dest)) {
      mkdirSync(dest, { recursive: true })
    }

    const files = readdirSync(src)

    for (const file of files) {
      copyRecursive(join(src, file), join(dest, file))
    }
  } else {
    copyFileSync(src, dest)
  }
}

console.log('📋 Copiando build da landing page...')

// Limpar diretório de destino (preservando `app/` e o .gitkeep)
if (existsSync(targetDir)) {
  const entries = readdirSync(targetDir)
  for (const entry of entries) {
    const fullPath = join(targetDir, entry)
    if (entry !== 'app' && entry !== '.gitkeep') {
      rmSync(fullPath, { recursive: true, force: true })
    }
  }
}

copyRecursive(sourceDir, targetDir)

console.log('✅ Landing page copiada para pocketbase/pb_public/')
