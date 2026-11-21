export default function TagList({ tags, row = false }: { tags: string[]; row?: boolean }) {
  return (
    <div className={row ? "tag-group tag-group-row" : "tag-group"}>
      {tags.map((tag) => (
        <span key={tag} className="tag">
          {tag}
        </span>
      ))}
    </div>
  );
}
