/**
 * What `/bff/user` tells the SPA about the current session.
 *
 * This is presentation data. It decides which menu items render and whose name appears in the
 * corner - it does not decide what the user may do. That decision lives in the API, which sees
 * the token rather than a JSON object the browser could have edited.
 */
export interface UserInfo {
  name: string | null;
  roles: string[];
}
