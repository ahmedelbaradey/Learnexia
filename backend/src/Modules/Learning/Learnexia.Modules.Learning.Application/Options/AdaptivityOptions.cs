// AdaptivityOptions lives in the Domain layer so that AdaptivityEngine (also Domain) can reference it
// without violating the Application → Domain dependency direction.
// This file is intentionally empty — consumers import from:
//   Learnexia.Modules.Learning.Domain.Services.AdaptivityOptions

// The DI binding in Infrastructure uses the Domain type directly via IOptions<AdaptivityOptions>.
