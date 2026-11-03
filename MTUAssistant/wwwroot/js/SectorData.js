export function formatRefresh(isoUtc) {
    const date = new Date(isoUtc);
    const pad = value => String(value).padStart(2, "0");
    return {
        date: `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`,
        tooltip: `Last completed refresh: ${new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "long" }).format(date)}`
    };
}
