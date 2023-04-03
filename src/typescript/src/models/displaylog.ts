export default interface DisplayLog {
  id: string;
  method: string;
  path: string;
  statusCode?: string;
  statusText?: string;
  contentLength?: string;
  data?: string;
  headers?: any;
}
