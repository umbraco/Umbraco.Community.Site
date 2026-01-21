export async function fetch(request: Promise<Response>) {
  let response: Response;
  let data;
  let error;

  try {
    response = await request;
    if (response.ok) {
      console.log("Promise resolved and HTTP status is successful");
      const text = await response.text();
      data = text ? JSON.parse(text) : undefined;
    } else {
      console.error("Promise resolved but HTTP status failed");
      error = response.status;
    }
  } catch (err) {
    console.error("Promise rejected", err);
    error = err;
  }

  return { data, error };
}
