// Браузерная сборка mammoth (docx → HTML) не везёт свои типы для этого сабпата —
// объявляем минимально нужный нам контракт (#5, клиентский предпросмотр Word).
declare module "mammoth/mammoth.browser" {
  export interface ConvertInput {
    arrayBuffer: ArrayBuffer
  }
  export interface ConvertResult {
    value: string
    messages: unknown[]
  }
  export function convertToHtml(input: ConvertInput): Promise<ConvertResult>
  const mammoth: { convertToHtml: typeof convertToHtml }
  export default mammoth
}
