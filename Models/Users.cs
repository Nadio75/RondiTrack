namespace RondiTrack.Models;

// This class represents a person who can join a stokvel.
public class User
{
    // The unique identifier for this user. Using Guid instead of an int
    // because we're generating IDs ourselves in-memory (no database auto-increment
    // to rely on yet), and Guid guarantees no collisions without coordination.
    public Guid Id { get; }

    // Private setter: this means code OUTSIDE this class can read the name
    // (User.Name) but cannot just do user.Name = "something" from a controller.
    // The only way to change it is through a method WE control (see Rename below).
    // This is what "the entity protects its own state" means in practice.
    public string Name { get; private set; }

    public string ContactNumber { get; private set; }

    // Constructor: this runs every time a new User is created.
    // We put our validation HERE instead of in the controller because this
    // way, it is IMPOSSIBLE to create a User object that breaks these rules —
    // not just "checked for" somewhere else that a developer might forget to call.
    public User(string name, string contactNumber)
    {
        // string.IsNullOrWhiteSpace catches null, empty string, AND strings
        // that are just spaces — all three would be "no real name" in practice.
        if (string.IsNullOrWhiteSpace(name))
        {
            // Throwing here means: this User object literally cannot exist
            // without a valid name. The exception stops construction dead.
            throw new ArgumentException("A user must have a name.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(contactNumber))
        {
            throw new ArgumentException("A user must have a contact number.", nameof(contactNumber));
        }

        // Only after both checks pass do we actually assign the fields.
        // If either check above had failed, we'd never reach these lines —
        // so there's no way to end up with a half-valid User.
        Id = Guid.NewGuid();       // generate a fresh unique ID right now
        Name = name;
        ContactNumber = contactNumber;
    }

    /* A controlled way to change the name later, instead of letting
     outside code set User.Name directly. This means every single place
     that tries to rename a user goes through the SAME validation —
     there's only one code path to keep correct, not one-per-controller.*/
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("A user's name cannot be blank.", nameof(newName));
        }

        Name = newName;
    }

    // Same pattern for updating contact info.
    public void UpdateContactNumber(string newContactNumber)
    {
        if (string.IsNullOrWhiteSpace(newContactNumber))
        {
            throw new ArgumentException("Contact number cannot be blank.", nameof(newContactNumber));
        }

        ContactNumber = newContactNumber;
    }
}