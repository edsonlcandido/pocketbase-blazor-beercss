#!/usr/bin/env node

/**
 * Baixa (se necessário) e sobe o binário do PocketBase.
 *
 * Funciona em Windows, Linux e macOS:
 *   - Windows: extrai o zip com `tar` (bsdtar) e usa `pocketbase.exe`
 *   - Linux/macOS: extrai com `unzip` e usa `pocketbase`
 */

import { spawn, execFileSync } from 'child_process'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'
import { existsSync, mkdirSync, chmodSync, unlinkSync, createWriteStream } from 'fs'
import https from 'https'

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const rootDir = join(__dirname, '..')
const pbDir = join(rootDir, 'pocketbase')

const VERSION = '0.40.5'
const IS_WINDOWS = process.platform === 'win32'

const pbBinary = join(pbDir, IS_WINDOWS ? 'pocketbase.exe' : 'pocketbase')

function getPocketBaseURL() {
  const platform = process.platform
  const arch = process.arch

  let os = ''
  let archSuffix = ''

  if (platform === 'linux') {
    os = 'linux'
  } else if (platform === 'darwin') {
    os = 'darwin'
  } else if (platform === 'win32') {
    os = 'windows'
  } else {
    throw new Error(`Plataforma não suportada: ${platform}`)
  }

  if (arch === 'x64') {
    archSuffix = 'amd64'
  } else if (arch === 'arm64') {
    archSuffix = 'arm64'
  } else {
    throw new Error(`Arquitetura não suportada: ${arch}`)
  }

  const fileName = `pocketbase_${VERSION}_${os}_${archSuffix}.zip`
  return `https://github.com/pocketbase/pocketbase/releases/download/v${VERSION}/${fileName}`
}

function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    https
      .get(url, (response) => {
        if (response.statusCode === 302 || response.statusCode === 301) {
          resolve(downloadFile(response.headers.location, dest))
          return
        }

        if (response.statusCode !== 200) {
          reject(new Error(`HTTP ${response.statusCode} ao baixar ${url}`))
          response.resume()
          return
        }

        const file = createWriteStream(dest)
        response.pipe(file)
        file.on('finish', () => {
          file.close()
          resolve()
        })
        file.on('error', reject)
      })
      .on('error', reject)
  })
}

function extract(zipPath) {
  if (IS_WINDOWS) {
    // bsdtar (vem com o Windows 10+) entende zip.
    execFileSync('tar', ['-xf', zipPath, '-C', pbDir])
  } else {
    execFileSync('unzip', ['-o', zipPath, '-d', pbDir])
  }
}

async function downloadAndExtractPocketBase() {
  console.log('🔄 Baixando PocketBase...')

  if (!existsSync(pbDir)) {
    mkdirSync(pbDir, { recursive: true })
  }

  const zipPath = join(pbDir, 'pocketbase.zip')
  const url = getPocketBaseURL()

  await downloadFile(url, zipPath)

  console.log('📦 Extraindo PocketBase...')
  extract(zipPath)
  unlinkSync(zipPath)

  if (!IS_WINDOWS) {
    chmodSync(pbBinary, '755')
  }

  console.log('✅ PocketBase instalado com sucesso!')
}

async function startPocketBase() {
  if (existsSync(pbBinary)) {
    console.log('✅ PocketBase já instalado')
  } else {
    await downloadAndExtractPocketBase()
  }

  // Bind em 0.0.0.0 por padrão pra funcionar atrás de reverse proxy
  // (Easypanel, Traefik, Codespaces, VSCode Server...).
  // Sobrescrever via PB_HTTP pra restringir a loopback.
  const http = process.env.PB_HTTP || '0.0.0.0:8090'

  console.log(`🚀 Iniciando PocketBase em ${http}...`)

  const pb = spawn(pbBinary, ['serve', `--http=${http}`], {
    cwd: pbDir,
    stdio: 'inherit',
  })

  pb.on('error', (error) => {
    console.error('Erro ao iniciar PocketBase:', error)
    process.exit(1)
  })

  pb.on('close', (code) => {
    console.log(`PocketBase encerrado com código ${code}`)
    process.exit(code)
  })
}

startPocketBase().catch((error) => {
  console.error('Erro:', error)
  process.exit(1)
})
