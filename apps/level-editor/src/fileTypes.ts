export function isImageFile(file: Pick<File, 'name' | 'type'>): boolean {
  return file.type.startsWith('image/') || /\.(png|jpe?g)$/i.test(file.name)
}
