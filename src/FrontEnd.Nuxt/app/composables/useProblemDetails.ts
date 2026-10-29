import { handleApiErrorImpl } from '~/utils/errorHandlerImpl'
import { useAppInsights } from './useAppInsights'

/**
 * Composable to retain latest details of problem reported to the user,
 * shared within the current Nuxt app.
 *
 * @returns Composable functions to manage details of problems reported to the user
 */
export function useProblemDetails() {
  const problem = useState<IProblemDetails | undefined>('problem-details', () => undefined)
  const ai = useAppInsights()
  const { logger } = useDebugLog('ProblemDetails')

  /**
   * Sends normalized problem details to Application Insights as a structured exception.
   * Uses the normalized IProblemDetails (not the raw error) so that all fields —
   * including traceId for W3C trace correlation — are available as queryable custom
   * dimensions in Azure Log Analytics.
   *
   * @param details - Normalized problem details to track
   */
  function trackProblemDetails(details: IProblemDetails, title: string) {
    ai.trackException(
      { exception: new Error(title) },
      {
        problemType: details.type,
        problemTitle: details.title,
        problemDetail: details.detail,
        httpStatus: details.status,
        traceId: (details as Record<string, unknown>)['traceId'],
      },
    )
  }

  /**
   * Clear the problem. Do this before each call to the backend, or before a
   * frontend action which might generate problems.
   */
  function clearError() {
    problem.value = undefined
  }

  /**
   * Set the problem based on an error generated locally by the frontend
   *
   * @param error Problem details set directly by the application
   *
   * @remarks
   * Tracks to App Insights with the fixed exception title 'Frontend Error' to
   * create a distinct, queryable category for errors explicitly reported via
   * this method (as opposed to backend errors caught by handleApiError).
   * The actual error title is still available as `Properties.problemTitle`.
   * Filter on ProblemId === 'Frontend Error' in Log Analytics
   * to find all such cases for review.
   */
  function setError(error: IProblemDetails) {
    problem.value = error
    logger.error('Problem details', error)
    trackProblemDetails(error, 'Frontend Error')
  }

  /**
   * Set the problem based on an error returned from the backend.
   *
   * @param error Error returned from API backend
   *
   * @remarks
   * It's useful to review these logs to discover potential changes we can
   * make it the application to avoid the error in the first place. In fact,
   * a good future step would be to separate errors we are happy to show to
   * the user (bad username/password), and ones we are not (operation not
   * permitted).
   */
  async function handleApiError(
    error: unknown,
    fallbackTitle: string = 'Unexpected Error',
    fallbackDetail?: string,
  ) {
    problem.value = handleApiErrorImpl(error, fallbackTitle, fallbackDetail)
    logger.error('Problem details', problem.value)
    trackProblemDetails(
      problem.value,
      fallbackDetail ? `${fallbackTitle}: ${fallbackDetail}` : fallbackTitle,
    )
  }

  return { problem, clearError, setError, handleApiError }
}
