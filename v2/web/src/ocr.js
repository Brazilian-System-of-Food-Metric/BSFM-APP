/* Carrega o Tesseract.js sob demanda e lê texto de uma imagem no próprio aparelho. */

let carregando = null

export function carregarTesseract() {
  if (window.Tesseract) return Promise.resolve(window.Tesseract)
  if (carregando) return carregando

  carregando = new Promise((resolver, rejeitar) => {
    const script = document.createElement('script')
    script.src = 'https://cdn.jsdelivr.net/npm/tesseract.js@5/dist/tesseract.min.js'
    script.onload = () => (window.Tesseract ? resolver(window.Tesseract) : rejeitar(new Error('Tesseract indisponível')))
    script.onerror = () => rejeitar(new Error('Falha ao carregar o motor de OCR'))
    document.head.appendChild(script)
  })

  return carregando
}

/**
 * Lê o texto de um arquivo de imagem.
 * @param {File} arquivo
 * @param {(progresso:number, status:string) => void} aoProgredir
 */
export async function lerTexto(arquivo, aoProgredir = () => {}) {
  const Tesseract = await carregarTesseract()

  const resultado = await Tesseract.recognize(arquivo, 'por', {
    logger: (m) => {
      if (m.status === 'recognizing text') {
        aoProgredir(Math.round((m.progress ?? 0) * 100), 'Lendo o texto…')
      } else if (m.status) {
        aoProgredir(0, m.status)
      }
    },
  })

  return (resultado?.data?.text ?? '').trim()
}
/** Converte o base64 devolvido pela câmera em Blob, para o OCR aceitar igual a um arquivo. */
export function base64ParaBlob(base64, tipo = 'image/jpeg') {
  const binario = atob(base64)
  const bytes = new Uint8Array(binario.length)
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i)
  return new Blob([bytes], { type: tipo })
}