import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getLogFiles, getLogFileContent, clearLogFile, type LogFileSummary } from '@/services/logsApi'

export type { LogFileSummary }

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useLogsStore = defineStore('logs', () => {
  const files = ref<LogFileSummary[]>([])
  const loadingFiles = ref(true)
  const filesError = ref<string | null>(null)

  const selectedFileName = ref<string | null>(null)
  const content = ref('')
  const loadingContent = ref(false)
  const contentError = ref<string | null>(null)

  /** Files are already ordered newest-first by the backend. */
  async function fetchFiles() {
    loadingFiles.value = true
    filesError.value = null
    try {
      files.value = await getLogFiles()
    } catch (err) {
      filesError.value = toMessage(err, 'Could not load the list of log files.')
    } finally {
      loadingFiles.value = false
    }
  }

  async function selectFile(fileName: string) {
    selectedFileName.value = fileName
    loadingContent.value = true
    contentError.value = null
    content.value = ''
    try {
      content.value = (await getLogFileContent(fileName)).content
    } catch (err) {
      contentError.value = toMessage(err, 'Could not load that log file.')
    } finally {
      loadingContent.value = false
    }
  }

  /** Clears the file's content on the backend, then reflects that locally — emptying the open
   * view if this was the selected file, and refreshing the file list for its updated timestamp. */
  async function clearFile(fileName: string) {
    await clearLogFile(fileName)
    if (selectedFileName.value === fileName) {
      content.value = ''
    }
    await fetchFiles()
  }

  return {
    files,
    loadingFiles,
    filesError,
    selectedFileName,
    content,
    loadingContent,
    contentError,
    fetchFiles,
    selectFile,
    clearFile,
  }
})
