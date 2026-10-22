import catalog from "./locales.json";

export const languages = [
  "English | en-us",
  "Spanish | es-es",
  "Chinese | zh-cn",
  "Chinese (Traditional) | zh-tw",
  "Japanese | ja-jp",
  "Swedish | sv-se",
  "Russian | ru-ru",
  "German | de-de",
  "Polish | pl-pl",
  "Turkish | tr-tr",
];

type Catalog = Record<string, Record<string, string>>;
const values = catalog as Catalog;

export function translate(language: string | undefined, key: string): string {
  const code = language?.split("|").at(-1)?.trim().toLowerCase() ?? "en-us";
  return values[code]?.[key] || values["en-us"]?.[key] || key;
}
