import { useEffect, useState } from "react";
import { getCurrentUser, type User } from "../../api/client";
import { errorMessage } from "../../lib/errorMessage";

export function useCurrentUser(isAuthenticated: boolean, onError: (message: string | null) => void) {
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      getCurrentUser()
        .then(setUser)
        .catch((err) => onError(errorMessage(err)));
    }
  }, [isAuthenticated]);

  return { user, setUser };
}
