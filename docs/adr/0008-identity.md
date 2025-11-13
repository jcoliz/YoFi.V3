# 0008. Identity system

Date: 2025-11-13

## Status

Draft

## Context

### Question

What software components and/or possible identity providers should we use for identity authentication and authorization?

In the past, I have rolled my own custom system. While I am happy with this, I've also received feedback that this is an unsafe practice, and should attempt to use as many off-the-shelf components as possible which have more security reviewers looking at them.

Some questions that come to mind:

* For the front-end, should I use NuxtAuth? This was recommended.
* ASP.NET includes some identity components. Can I leverage those? I know them well. Can I combine them with NuxtAuth??
* Should I use an external identity providers?
* How do I handle authorization? Certain users will have access to certain accounts, and this is information that is unique to the app, so I'll have to store it here.
* Are there any examples of good existing systems that combine Nuxt frontend with ASP.NET backend for identity?

## Decision

What is the change that we're proposing and/or doing?

## Consequences

What becomes easier or more difficult to do because of this change?