export default function Loading() {
  return (
    <div role="status" className="animate-pulse space-y-4">
      <div className="h-8 w-48 rounded bg-gray-200" />
      <div className="h-32 rounded bg-gray-200" />
      <p className="text-gray-600">Loading your RondiTrack information...</p>
    </div>
  );
}
